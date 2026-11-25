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

    // The collector reader should be able to count the number of predicted trips in the latest snapshot,
    [Fact]
    public void GetRouteDayReliability_CountsPerRoute_AndHidesDaysWithTooFewPolls()
    {
        CollectorDatabase db = new CollectorDatabase(_dbPath);
        db.CreateTables();

        PredictedTrip Trip(string id, string route, string status)
        {
            return new PredictedTrip { StartDate = "20250101", TripId = id, RouteId = route, ScheduleRelationship = status };
        }

        db.RecordPredictedTrips(new List<PredictedTrip>
        {
            Trip("T1", "3_N6", "Scheduled"),   // seen below
            Trip("T2", "3_N6", "Scheduled"),   // never seen: silent
            Trip("T3", "3_N6", "Canceled"),    // never seen, declared cancelled
            Trip("T4", "OTHER", "Scheduled")   // a second route
        }, 1000);
        db.RecordObservedTrips(new List<ObservedTrip>
        {
            new ObservedTrip { StartDate = "20250101", TripId = "T1", RouteId = "3_N6" }
        }, 1000);
        db.LogPoll("Vehicles", true, 200, 10, null, 1735700000);      // one successful poll per feed that day
        db.LogPoll("TripUpdates", true, 200, 10, null, 1735700000);

        CollectorReader reader = new CollectorReader(_dbPath);
        List<RouteDayReliability> rows = reader.GetRouteDayReliability(5000, 1);

        Assert.Equal(2, rows.Count);   // one row per route
        RouteDayReliability bus = rows.Single(r => r.RouteId == "3_N6");
        Assert.Equal(3, bus.Predicted);
        Assert.Equal(1, bus.Cancelled);
        Assert.Equal(2, bus.NeverSeen);
        Assert.Equal(1, bus.Silent);

        Assert.Empty(reader.GetRouteDayReliability(5000, 2));   // two polls required: the day is hidden
    }

}
