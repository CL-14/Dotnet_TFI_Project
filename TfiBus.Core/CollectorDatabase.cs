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

            );
             -- Where each bus is right now. Replaced on every vehicles poll. To read from here instead of fetching every time
            CREATE TABLE IF NOT EXISTS vehicle_latest (
              vehicle_id TEXT NOT NULL PRIMARY KEY,
              trip_id TEXT,
              route_id TEXT,
              start_date TEXT,
              start_time TEXT,
              latitude REAL NOT NULL,
              longitude REAL NOT NULL,
              bearing REAL,
              position_time INTEGER,       -- Unix time the vehicle reported this position
              polled_at INTEGER NOT NULL   -- Unix time of the poll that stored it
            );

            -- Alerts active right now. Replaced on every alerts poll. Same as above
            CREATE TABLE IF NOT EXISTS alert_current (
              alert_id TEXT NOT NULL PRIMARY KEY,
              header TEXT,
              description TEXT,
              route_ids TEXT,              -- affected route ids, comma-separated
              stop_ids TEXT,               -- affected stop ids, comma-separated
              active_from INTEGER,
              active_to INTEGER,
              severity TEXT,
              polled_at INTEGER NOT NULL
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

  //writer section


  //Converts C# null into the databse's own "no value" marker (DBNull).
  //Parameters cannot be given plain null, so every optional value goes through this

  private static object DbValue(object? value)
  {
    return value ?? DBNull.Value;
  }

  //returns one poll of the trip-updates feed. A new trip gets a row; a known trip gets
  //last_seen, seen_count, its latest delay and its highest delay so far updated
  public void RecordPredictedTrips(List<PredictedTrip>trips, long polledAtUnix)
  {
    using SqliteConnection connection = new SqliteConnection(_connectionString);
    connection.Open();
    using SqliteTransaction transaction = connection.BeginTransaction();
    using SqliteCommand command = connection.CreateCommand();

    //COALESCE(a, b) means "a, or b if a has no value". It keeps the last known delay
    //when a poll has none, and lets MAX work when either delay is missing
    command.CommandText = @"
            INSERT INTO trip_predicted
              (start_date, trip_id, route_id, start_time, first_seen, last_seen, seen_count,
               last_delay_s, max_delay_s, schedule_relationship)
            VALUES
              ($startDate, $tripId, $routeId, $startTime, $polledAt, $polledAt, 1,
               $delay, $delay, $relationship)
            ON CONFLICT (start_date, trip_id) DO UPDATE SET
              last_seen = excluded.last_seen,
              seen_count = seen_count + 1,
              last_delay_s = COALESCE(excluded.last_delay_s, last_delay_s),
              max_delay_s = MAX(COALESCE(max_delay_s, excluded.last_delay_s),
                                COALESCE(excluded.last_delay_s, max_delay_s)),
              schedule_relationship = excluded.schedule_relationship;";

    foreach (PredictedTrip trip in trips)
    {
      command.Parameters.Clear(); //this removes the previous trip's values
      command.Parameters.AddWithValue("$startDate", trip.StartDate);
      command.Parameters.AddWithValue("$tripId", trip.TripId);
      command.Parameters.AddWithValue("$routeId", trip.RouteId);
      command.Parameters.AddWithValue("$startTime", trip.StartTime);
      command.Parameters.AddWithValue("$polledAt", polledAtUnix);
      command.Parameters.AddWithValue("$delay", DbValue(trip.DelaySeconds));
      command.Parameters.AddWithValue("$relationship", trip.ScheduleRelationship);
      command.ExecuteNonQuery();
    }

    transaction.Commit();

  }

  //replaces the whole vehicle_latest table with the vehicles from this poll
  //delete and insert happen in one transaction, so  reader never sees a half-empty table
  //and vehicles that left the feed disappear

  public void ReplaceLatestVehicles(List<VehicleSnapshot> vehicles, long polledAtUnix)
  {
    using SqliteConnection connection = new SqliteConnection(_connectionString);
    connection.Open();
    using SqliteTransaction transaction = connection.BeginTransaction();
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;

    command.CommandText = "DELETE FROM vehicle_latest";
    command.ExecuteNonQuery();

    //INSERT OR REPLACE overwrites a row with the same vehicle_id instead of failing,
    //in case the feed lists a vehicle twice.
    command.CommandText = @"
            INSERT OR REPLACE INTO vehicle_latest
              (vehicle_id, trip_id, route_id, start_date, start_time,
               latitude, longitude, bearing, position_time, polled_at)
            VALUES
              ($vehicleId, $tripId, $routeId, $startDate, $startTime,
               $lat, $lon, $bearing, $positionTime, $polledAt);";

    foreach (VehicleSnapshot vehicle in vehicles)
    {
      command.Parameters.Clear();
      command.Parameters.AddWithValue("$vehicleId", vehicle.VehicleId);
      command.Parameters.AddWithValue("$tripId", vehicle.TripId);
      command.Parameters.AddWithValue("$routeId", vehicle.RouteId);
      command.Parameters.AddWithValue("$startDate", vehicle.StartDate);
      command.Parameters.AddWithValue("$startTime", vehicle.StartTime);
      command.Parameters.AddWithValue("$lat", vehicle.Latitude);
      command.Parameters.AddWithValue("$lon", vehicle.Longitude);
      command.Parameters.AddWithValue("$bearing", vehicle.Bearing);
      command.Parameters.AddWithValue("$positionTime", vehicle.PositionTime);
      command.Parameters.AddWithValue("$polledAt", polledAtUnix);
      command.ExecuteNonQuery();
    }

    transaction.Commit();

  }

  // Replaces the whole alert_current table with the alerts from this poll, same idea as above
  public void ReplaceCurrentAlerts(List<AlertRecord> alerts, long polledAtUnix)
  {
    using SqliteConnection connection = new SqliteConnection(_connectionString);
    connection.Open();
    using SqliteTransaction transaction = connection.BeginTransaction();
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;

    command.CommandText = "DELETE FROM alert_current";
    command.ExecuteNonQuery();

    command.CommandText = @"
            INSERT OR REPLACE INTO alert_current
              (alert_id, header, description, route_ids, stop_ids,
               active_from, active_to, severity, polled_at)
            VALUES
              ($alertId, $header, $description, $routeIds, $stopIds,
               $from, $to, $severity, $polledAt);";

    foreach (AlertRecord alert in alerts)
    {
      command.Parameters.Clear();
      command.Parameters.AddWithValue("$alertId", alert.AlertId);
      command.Parameters.AddWithValue("$header", alert.Header);
      command.Parameters.AddWithValue("$description", alert.Description);
      command.Parameters.AddWithValue("$routeIds", alert.RouteIds);
      command.Parameters.AddWithValue("$stopIds", alert.StopIds);
      command.Parameters.AddWithValue("$from", DbValue(alert.ActiveFrom));   // may be null
      command.Parameters.AddWithValue("$to", DbValue(alert.ActiveTo));
      command.Parameters.AddWithValue("$severity", alert.Severity);
      command.Parameters.AddWithValue("$polledAt", polledAtUnix);
      command.ExecuteNonQuery();
    }

    transaction.Commit();
  }

  // Adds one row to poll_log: which feed was polled, whether it worked, and why not if it failed.
  public void LogPoll(string feed, bool ok, int? httpStatus, int entities, string? error, long polledAtUnix)
  {
    using SqliteConnection connection = new SqliteConnection(_connectionString);
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
            INSERT INTO poll_log (polled_at, feed, ok, http_status, entities, error)
            VALUES ($polledAt, $feed, $ok, $status, $entities, $error);";
    command.Parameters.AddWithValue("$polledAt", polledAtUnix);
    command.Parameters.AddWithValue("$feed", feed);
    command.Parameters.AddWithValue("$ok", ok ? 1 : 0);   // SQLite has no true/false type; 1 and 0 are used
    command.Parameters.AddWithValue("$status", DbValue(httpStatus));
    command.Parameters.AddWithValue("$entities", entities);
    command.Parameters.AddWithValue("$error", DbValue(error));
    command.ExecuteNonQuery();
  }

}
