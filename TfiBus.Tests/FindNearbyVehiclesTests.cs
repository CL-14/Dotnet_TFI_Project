using TfiBus.Core;

namespace TfiBus.Tests;

public class FindNearbyVehiclesTests
{
    [Fact]
    public void FindNearbyVehicles_FiltersByRadius_AndSortsNearestFirst()
    {
        Dictionary<string, Stop> stops = new Dictionary<string, Stop>
        {
            ["S1"] = new Stop { StopCode = "S1", Lat = 53.40, Lon = -6.26 }
        };
        List<VehicleSnapshot> vehicles = new List<VehicleSnapshot>
        {
            new VehicleSnapshot { VehicleId = "mid",  Latitude = 53.405, Longitude = -6.26 },   // about 0.56 km
            new VehicleSnapshot { VehicleId = "near", Latitude = 53.401, Longitude = -6.26 },   // about 0.11 km
            new VehicleSnapshot { VehicleId = "far",  Latitude = 53.50,  Longitude = -6.26 }    // about 11 km
        };

        List<NearbyVehicle> result = BusLogic.FindNearbyVehicles("S1", 1.0, stops, vehicles);

        Assert.Equal(2, result.Count);
        Assert.Equal("near", result[0].VehicleId);
        Assert.Equal("mid", result[1].VehicleId);
    }
}
