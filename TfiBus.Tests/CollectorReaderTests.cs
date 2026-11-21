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
}
