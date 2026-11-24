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

    // All vehicles in the latest snapshot. SqliteDataReader hands back one row at a time
    // columns are read by their position in the SELECT list.
    public List<VehicleSnapshot> GetLatestVehicles()
    {
        List<VehicleSnapshot> result = new List<VehicleSnapshot>();
        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"SELECT vehicle_id, trip_id, route_id, start_date, start_time,
                                       latitude, longitude, bearing, position_time
                                FROM vehicle_latest";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())   // moves to the next row; false when there are no more
        {
            VehicleSnapshot v = new VehicleSnapshot();
            v.VehicleId = reader.GetString(0);
            v.TripId = reader.IsDBNull(1) ? "" : reader.GetString(1);   // IsDBNull checks for a database NULL
            v.RouteId = reader.IsDBNull(2) ? "" : reader.GetString(2);
            v.StartDate = reader.IsDBNull(3) ? "" : reader.GetString(3);
            v.StartTime = reader.IsDBNull(4) ? "" : reader.GetString(4);
            v.Latitude = reader.GetDouble(5);
            v.Longitude = reader.GetDouble(6);
            v.Bearing = reader.IsDBNull(7) ? 0 : reader.GetDouble(7);
            v.PositionTime = reader.IsDBNull(8) ? 0 : reader.GetInt64(8);
            result.Add(v);
        }
        return result;
    }

    // Bus reliability per finished day, newest first. The day filter is a stand-in until route types
    // from the static schedule are used: it excludes rail codes (with a dash) and Luas (starting 10000).
    public List<DailyReliability> GetDailyReliability(int daysBack, int minPollsPerFeed)
    {
        List<DailyReliability> result = new List<DailyReliability>();
        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            WITH polls AS (
                -- successful polls per day and per feed, from the poll log (polled_at is Unix time, UTC)
                SELECT strftime('%Y%m%d', polled_at, 'unixepoch') AS poll_day,
                       SUM(CASE WHEN feed = 'Vehicles' AND ok = 1 THEN 1 ELSE 0 END) AS vehicle_polls,
                       SUM(CASE WHEN feed = 'TripUpdates' AND ok = 1 THEN 1 ELSE 0 END) AS trip_polls
                FROM poll_log
                GROUP BY poll_day
            )
            SELECT p.start_date,
                   COUNT(*),
                   SUM(CASE WHEN p.schedule_relationship = 'Canceled' THEN 1 ELSE 0 END),
                   SUM(CASE WHEN o.trip_id IS NULL THEN 1 ELSE 0 END),
                   SUM(CASE WHEN o.trip_id IS NULL AND p.schedule_relationship <> 'Canceled' THEN 1 ELSE 0 END)
            FROM trip_predicted p
            JOIN polls c ON c.poll_day = p.start_date
                        AND c.vehicle_polls >= $minPolls AND c.trip_polls >= $minPolls
            LEFT JOIN trip_observed o ON o.start_date = p.start_date AND o.trip_id = p.trip_id
            WHERE p.start_date >= strftime('%Y%m%d', 'now', $offset)
              AND p.start_date < strftime('%Y%m%d', 'now')
              AND p.route_id NOT LIKE '%-%' AND p.route_id NOT LIKE '10000%'
            GROUP BY p.start_date ORDER BY p.start_date DESC";
        command.Parameters.AddWithValue("$offset", $"-{daysBack} days");
        command.Parameters.AddWithValue("$minPolls", minPollsPerFeed);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            DailyReliability day = new DailyReliability();
            day.StartDate = reader.GetString(0);
            day.Predicted = reader.GetInt32(1);
            day.Cancelled = reader.GetInt32(2);
            day.NeverSeen = reader.GetInt32(3);
            day.Silent = reader.GetInt32(4);
            result.Add(day);
        }
        return result;
    }
}
