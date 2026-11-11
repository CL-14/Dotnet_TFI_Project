using System.Data.Common;
using Microsoft.Data.Sqlite;
using TfiBus.Core;

namespace TfiBus.Tests;

//xUnit will call dispose() after every test, which deletes the temp db
public class CollectorDatabaseTests : IDisposable
{
    private readonly string _dbPath;

    //run before each test, it picks up a unique temp file name so tests never share a db
    public CollectorDatabaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"collector-test-{Guid.NewGuid}.db");
    }

    //run after a test, Ms data Sqlite keeps connections in a pool so they are released first, otherwise the file can stay locked.
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        //WAL mode creates "-wal" and "-shm" side files, this is to remove all three
        foreach (string suffix in new[] { "", "-wall", "-shm" })
        {
            if (File.Exists(_dbPath + suffix)) { File.Delete(_dbPath + suffix); }

        }

    }
    //helper to run a scalar query and return the result as a long, used for counting rows and checking values
    private long ScalarQuery(string sql)
    {
        using SqliteConnection connection = new SqliteConnection($"Data source={_dbPath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!; // "!" to clarify that the result is never null here

    }

    //helper to make a trip with a given id, used in several tests
    private static ObservedTrip MakeTrip(string tripId)
    {
        return new ObservedTrip
        {
            StartDate = "20261102",
            TripId = tripId,
            RouteId = "R1",
            StartTime = "08:00:00",
            VehicleId = "V1"
        };

    }

    //test that recording the same trip twice updates the row instead of adding a new one
    [Fact]
    public void RecordObservedTrips_SameTripTwice_UpdatesTheRowInsteadOfAddingOne()
    {
        //a new bd with the tables created
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();
        List<ObservedTrip> trips = new List<ObservedTrip> { MakeTrip("T1") };

        //record the same trip in two polls, 120 seconds apart
        db.RecordObservedTrips(trips, 1000);
        db.RecordObservedTrips(trips, 1120);

        //stil one row, counted twice, with first and last times keptapart
        Assert.Equal(1, ScalarQuery("SELECT COUNT(*) FROM trip_observed"));
        Assert.Equal(2, ScalarQuery("SELECT seen_count FROM trip_observed WHERE trip_id = 'T1'"));
        Assert.Equal(1000, ScalarQuery("SELECT first_seen FROM trip_observed WHERE trip_id = 'T1'"));
        Assert.Equal(1120, ScalarQuery("SELECT last_seen FROM trip_observed WHERE trip_id = 'T1'"));

    }

    //test that recording two different trips creates two rows, and that CreateTables can be called twice without error
    [Fact]
    public void RecordObservedTrips_DifferentTrips_GetSeparateRows_AndCreateTablesCanRunTwice()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();
        db.CreateTables();

        db.RecordObservedTrips(new List<ObservedTrip> { MakeTrip("T1"), MakeTrip("T2") }, 1000);

        Assert.Equal(2, ScalarQuery("SELECT COUNT(*) FROM trip_observed"));
    }

    //test that CreateTables creates all five tables, by counting them in sqlite_master
    [Fact]
    public void CreateTables_CreatesAllFiveTables()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();

        // sqlite_master lists every table, counting the five names confirms they all exist.
        long count = ScalarQuery(@"SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name IN
            ('trip_observed', 'trip_predicted', 'poll_log', 'vehicle_latest', 'alert_current')");

        Assert.Equal(5, count);
    }

    //test that recording predicted trips tracks the last and max delay, and keeps the last delay when a poll has no delay
    [Fact]
    public void RecordPredictedTrips_TracksLastAndMaxDelay_AndKeepsLastDelayWhenMissing()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();

        PredictedTrip At(int? delay)
        {
            return new PredictedTrip
            {
                StartDate = "20261112",
                TripId = "T1",
                RouteId = "R1",
                StartTime = "08:00:00",
                ScheduleRelationship = "Scheduled",
                DelaySeconds = delay
            };
        }

        db.RecordPredictedTrips(new List<PredictedTrip> { At(100) }, 1000);
        db.RecordPredictedTrips(new List<PredictedTrip> { At(300) }, 1120);
        db.RecordPredictedTrips(new List<PredictedTrip> { At(50) }, 1240);
        db.RecordPredictedTrips(new List<PredictedTrip> { At(null) }, 1360);   // no delay this poll

        Assert.Equal(1, ScalarQuery("SELECT COUNT(*) FROM trip_predicted"));
        Assert.Equal(4, ScalarQuery("SELECT seen_count FROM trip_predicted"));
        Assert.Equal(300, ScalarQuery("SELECT max_delay_s FROM trip_predicted"));
        Assert.Equal(50, ScalarQuery("SELECT last_delay_s FROM trip_predicted"));   // the null poll did not erase it
    }

    //test that replacing the latest vehicles removes any previous rows for vehicles that are no longer in the feed
    [Fact]
    public void ReplaceLatestVehicles_ReplacesThePreviousSnapshot()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();

        VehicleSnapshot Vehicle(string id)
        {
            return new VehicleSnapshot { VehicleId = id, TripId = "T1", Latitude = 53.4, Longitude = -6.26 };
        }

        db.ReplaceLatestVehicles(new List<VehicleSnapshot> { Vehicle("V1"), Vehicle("V2") }, 1000);
        Assert.Equal(2, ScalarQuery("SELECT COUNT(*) FROM vehicle_latest"));

        db.ReplaceLatestVehicles(new List<VehicleSnapshot> { Vehicle("V2") }, 1120);   // V1 left the feed

        Assert.Equal(1, ScalarQuery("SELECT COUNT(*) FROM vehicle_latest"));
        Assert.Equal(0, ScalarQuery("SELECT COUNT(*) FROM vehicle_latest WHERE vehicle_id = 'V1'"));
        Assert.Equal(1120, ScalarQuery("SELECT polled_at FROM vehicle_latest WHERE vehicle_id = 'V2'"));
    }

    //test that replacing the current alerts removes any previous rows for alerts that are no longer in the feed, and that missing active periods are stored as null
    [Fact]
    public void ReplaceCurrentAlerts_ReplacesPreviousAlerts_AndStoresMissingPeriodAsNull()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();

        db.ReplaceCurrentAlerts(new List<AlertRecord>
        {
            new AlertRecord { AlertId = "A1", ActiveFrom = 100, ActiveTo = 200 },
            new AlertRecord { AlertId = "A2" }   // no period
        }, 1000);
        Assert.Equal(2, ScalarQuery("SELECT COUNT(*) FROM alert_current"));
        Assert.Equal(1, ScalarQuery("SELECT COUNT(*) FROM alert_current WHERE active_from IS NULL"));

        db.ReplaceCurrentAlerts(new List<AlertRecord> { new AlertRecord { AlertId = "A3" } }, 1120);

        Assert.Equal(1, ScalarQuery("SELECT COUNT(*) FROM alert_current"));
        Assert.Equal(0, ScalarQuery("SELECT COUNT(*) FROM alert_current WHERE alert_id = 'A1'"));
    }

    //test that logging a poll creates a row with the correct values, and that both success and failure rows are recorded
    [Fact]
    public void LogPoll_WritesSuccessAndFailureRows()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();

        db.LogPoll("vehicles", true, 200, 700, null, 1000);
        db.LogPoll("alerts", false, 429, 0, "rate limited", 1060);

        Assert.Equal(2, ScalarQuery("SELECT COUNT(*) FROM poll_log"));
        Assert.Equal(1, ScalarQuery("SELECT COUNT(*) FROM poll_log WHERE ok = 0"));
        Assert.Equal(429, ScalarQuery("SELECT http_status FROM poll_log WHERE ok = 0"));
    }

}
