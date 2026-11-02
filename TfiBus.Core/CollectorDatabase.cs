using Microsoft.Data.Sqlite;

namespace TfiBus.Core;

//holds one trip seen in the vehicle feed during one poll, a plain data holder.
public class ObservedTrip
{
    public string StartDate { get; set; } = "";
    public string TripId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string VehicleId { get; set; } = "";
}

public class CollectorDatabase
{
    //text that tells Sqlite which file to open
    private readonly string _connectionString;

    //runs on "new collectorDatabase(path)" and remmebers where the database file is
    public CollectorDatabase(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    //creates the three tables if theyd o not exist yet; safe to call on every start
    public void CreateTables()
    {
        //the "using" closes the connection when the method ends
        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();

        //WAL mode allows to read the data while the collector keeps writing
        //the setting is stored in the database file, so it only needs to run once

        command.CommandText = @"
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS trip_observed (
              start_date TEXT NOT NULL,
              trip_id TEXT NOT NULL,
              route_id TEXT,
              start_time TEXT,
              vehicle_id TEXT,
              first_seen INTEGER NOT NULL,
              last_seen INTEGER NOT NULL,
              seen_count INTEGER NOT NULL,
              PRIMARY KEY (start_date, trip_id)
            );

            CREATE TABLE IF NOT EXISTS trip_predicted (
              start_date TEXT NOT NULL,
              trip_id TEXT NOT NULL,
              route_id TEXT,
              start_time TEXT,
              first_seen INTEGER NOT NULL,
              last_seen INTEGER NOT NULL,
              seen_count INTEGER NOT NULL,
              last_delay_s INTEGER,
              max_delay_s INTEGER,
              schedule_relationship TEXT,
              PRIMARY KEY (start_date, trip_id)
            );

            CREATE TABLE IF NOT EXISTS poll_log (
              polled_at INTEGER NOT NULL,
              feed TEXT NOT NULL,
              ok INTEGER NOT NULL,
              http_status INTEGER,
              entities INTEGER,
              error TEXT
            );";


        command.ExecuteNonQuery(); //this runs the SQL; NonQuery returns no rows
    }

    //save every trip from one poll in a single transaction.
    //new trip: insert a row. Trip already stored: updates last_seen and adds 1 to seen_count
    public void RecordObservedTrips(List<ObservedTrip> trips, long polledAtUnix)
    {
        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();

        //a transaction groups de 1100 writes into one disk commit. without it sqlite commits each row separately
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;

        //$name placeholders are filled below
        command.CommandText = @"
            INSERT INTO trip_observed
              (start_date, trip_id, route_id, start_time, vehicle_id, first_seen, last_seen, seen_count)
            VALUES
              ($startDate, $tripId, $routeId, $startTime, $vehicleId, $polledAt, $polledAt, 1)
            ON CONFLICT (start_date, trip_id) DO UPDATE SET
              last_seen = excluded.last_seen,
              seen_count = seen_count + 1,
              vehicle_id = excluded.vehicle_id;";

        //create each parameter once, then change its value for every trip
        SqliteParameter pStartDate = command.Parameters.Add("$startDate", SqliteType.Text);
        SqliteParameter pTripId = command.Parameters.Add("$tripId", SqliteType.Text);
        SqliteParameter pRouteId = command.Parameters.Add("$routeId", SqliteType.Text);
        SqliteParameter pStartTime = command.Parameters.Add("$startTime", SqliteType.Text);
        SqliteParameter pVehicleId = command.Parameters.Add("$vehicleId", SqliteType.Text);
        SqliteParameter pPolledAt = command.Parameters.Add("$polledAt", SqliteType.Integer);
        pPolledAt.Value = polledAtUnix;

        foreach (ObservedTrip trip in trips)
        {
            pStartDate.Value = trip.StartDate;
            pTripId.Value = trip.TripId;
            pRouteId.Value = trip.RouteId;
            pStartTime.Value = trip.StartTime;
            pVehicleId.Value = trip.VehicleId;
            command.ExecuteNonQuery();
        }

        transaction.Commit(); //make the writes permanent.
    }


}
