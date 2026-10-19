namespace TfiBus.Core;

// Holds the result for one vehicle that is inside the search radius.
public class NearbyVehicle
{
    public string VehicleId { get; set; } = "";
    public double DistanceKm { get; set; }
}
