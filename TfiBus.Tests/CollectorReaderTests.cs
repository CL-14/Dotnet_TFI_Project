using TfiBus.Core;
public class CollectorReaderTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"reader-test-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            if (File.Exists(_dbPath + suffix)) { File.Delete(_dbPath + suffix); }
        }
    }

    // The collector reader should be able to count the number of vehicles in the latest snapshot,
    [Fact]
    public void Reader_CountsVehiclesAndFindsLastSuccessfulPoll()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();
        db.ReplaceLatestVehicles(new List<VehicleSnapshot>
        {
            new VehicleSnapshot { VehicleId = "V1" }, new VehicleSnapshot { VehicleId = "V2" }
        }, 1000);
        db.LogPoll("Vehicles", true, 200, 2, null, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        CollectorReader reader = new CollectorReader(_dbPath);

        Assert.Equal(2, reader.CountLatestVehicles());
        Assert.Equal(0, reader.MinutesSinceLastSuccessfulPoll());   // the poll was just now
    }

    // The daily reliability figures should separate cancelled trips from silent trips, and ignore rail trips.
    [Fact]
    public void GetDailyReliability_SeparatesCancelledFromSilent_AndIgnoresRail()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();

        PredictedTrip Trip(string id, string route, string status)
        {
            return new PredictedTrip { StartDate = "20250101", TripId = id, RouteId = route, ScheduleRelationship = status };
        }

        db.RecordPredictedTrips(new List<PredictedTrip>
        {
            Trip("T1", "3_N6", "Scheduled"),            // seen below
            Trip("T2", "3_N6", "Scheduled"),            // never seen: silent
            Trip("T3", "3_N6", "Canceled"),             // never seen, but declared cancelled
            Trip("T4", "BRAY-HOWTH-O", "Scheduled")     // rail: must be ignored
        }, 1000);
        db.RecordObservedTrips(new List<ObservedTrip>
        {
            new ObservedTrip { StartDate = "20250101", TripId = "T1", RouteId = "3_N6" }
        }, 1000);

        // The day needs successful polls on both feeds to count as complete.
        db.LogPoll("Vehicles", true, 200, 10, null, 1735700000);      // 1 Jan 2025, 03:33 UTC
        db.LogPoll("TripUpdates", true, 200, 10, null, 1735700000);

        List<DailyReliability> days = new CollectorReader(_dbPath).GetDailyReliability(5000, 1);

        Assert.Single(days);
        Assert.Equal(3, days[0].Predicted);
        Assert.Equal(1, days[0].Cancelled);
        Assert.Equal(2, days[0].NeverSeen);
        Assert.Equal(1, days[0].Silent);
    }

    // The daily reliability figures should hide days with too few successful polls.
    [Fact]
    public void GetDailyReliability_HidesDaysWithTooFewSuccessfulPolls()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();
        db.RecordPredictedTrips(new List<PredictedTrip>
        {
            new PredictedTrip { StartDate = "20250101", TripId = "T1", RouteId = "3_N6", ScheduleRelationship = "Scheduled" }
        }, 1000);
        db.LogPoll("Vehicles", true, 200, 10, null, 1735700000);      // only one poll per feed that day
        db.LogPoll("TripUpdates", true, 200, 10, null, 1735700000);

        CollectorReader reader = new CollectorReader(_dbPath);

        Assert.Single(reader.GetDailyReliability(5000, 1));   // one poll is enough when the minimum is 1
        Assert.Empty(reader.GetDailyReliability(5000, 2));    // two are required, so the day is hidden
    }
}
