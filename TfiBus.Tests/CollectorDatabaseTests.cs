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
        foreach (string suffix in new [] {"", "-wall", "-shm"})
        {
            if (File.Exists(_dbPath + suffix)) {File.Delete(_dbPath + suffix); }

        }

    }

    private long ScalarQuery(string sql)
    {
        using SqliteConnection connection = new SqliteConnection($"Data source={_dbPath}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!; // "!" to clarify that the result is never null here

    }

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

    [Fact]
    public void RecordObservedTrips_SameTripTwice_UpdatesTheRowInsteadOfAddingOne()
    {
        //a new bd with the tables created
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();
        List<ObservedTrip> trips = new List<ObservedTrip> {MakeTrip("T1")};

        //record the same trip in two polls, 120 seconds apart
        db.RecordObservedTrips(trips, 1000);
        db.RecordObservedTrips(trips,1120);

        //stil one row, counted twice, with first and last times keptapart
        Assert.Equal(1, ScalarQuery("SELECT COUNT(*) FROM trip_observed"));
        Assert.Equal(2, ScalarQuery("SELECT seen_count FROM trip_observed WHERE trip_id = 'T1'"));
        Assert.Equal(1000, ScalarQuery("SELECT first_seen FROM trip_observed WHERE trip_id = 'T1'"));
        Assert.Equal(1120, ScalarQuery("SELECT last_seen FROM trip_observed WHERE trip_id = 'T1'"));

    }


    [Fact]
    public void RecordObservedTrips_DifferentTrips_GetSeparateRows_AndCreateTablesCanRunTwice()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();
        db.CreateTables();  

        db.RecordObservedTrips(new List<ObservedTrip> { MakeTrip("T1"), MakeTrip("T2") }, 1000);

        Assert.Equal(2, ScalarQuery("SELECT COUNT(*) FROM trip_observed"));
    }

}
