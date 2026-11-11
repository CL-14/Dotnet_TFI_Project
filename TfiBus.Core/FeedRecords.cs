namespace TfiBus.Core;

//the full data holders to be mapped into the DB

//one vehicle's position at the moment of a vehicle-feed poll
public class VehicleSnapshot
{
    public string VehicleId { get; set; } = "";
    public string TripId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string StartTime { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Bearing { get; set; }

    // Unix time, seconds since 1 January 1970, the form the feeds and the database both use.
    public long PositionTime { get; set; }
}

// One trip that has a prediction in the trip-updates feed.
public class PredictedTrip
{
    public string StartDate { get; set; } = "";
    public string TripId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string ScheduleRelationship { get; set; } = "";

    // It is null when the trip has no stop updates, which is different from "0 seconds late".
    public int? DelaySeconds { get; set; }
}

//one active service alert
public class AlertRecord
{
    public string AlertId {get; set;} = "";
    public string Header {get; set;} = "";
    public string Description { get; set; } = "";
    public string RouteIds { get; set; } = "";   // comma-separated
    public string StopIds { get; set; } = "";    // comma-separated
    public long? ActiveFrom { get; set; }        // Unix time, null if the alert gives none
    public long? ActiveTo { get; set; }
    public string Severity { get; set; } = "";
}
