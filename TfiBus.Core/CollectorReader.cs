using Microsoft.Data.Sqlite;

namespace TfiBus.Core;

// Reads the collector's database for the website.
public class CollectorReader
{
    private readonly string _connectionString;

    public CollectorReader(string databasePath)
    {
        // Mode=ReadOnly means this connection can read but never write.
        _connectionString = $"Data Source={databasePath};Mode=ReadOnly";
    }

    // How many vehicles are currently stored in vehicle_latest.
    public long CountLatestVehicles()
    {
        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM vehicle_latest";
        return (long)command.ExecuteScalar()!;
    }

    // Minutes since the last successful poll, or null if there has been none.
    public long? MinutesSinceLastSuccessfulPoll()
    {
        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT (strftime('%s','now') - MAX(polled_at)) / 60 FROM poll_log WHERE ok = 1";
        object? result = command.ExecuteScalar();
        // MAX over zero rows gives a database NULL, which arrives as DBNull.
        if (result == null || result is DBNull) { return null; }
        return Convert.ToInt64(result);
    }
}
