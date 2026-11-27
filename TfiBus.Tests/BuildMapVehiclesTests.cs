using TfiBus.Core;

namespace TfiBus.Tests;

// Tests for BusLogic.BuildMapVehicles, which decides what the live map shows.
public class BuildMapVehiclesTests
{
    // Two known routes: one bus route and one rail route.
    private static Dictionary<string, RouteInfo> SampleRoutes()
    {
        return new Dictionary<string, RouteInfo>
        {
            ["R39A"] = new RouteInfo { RouteId = "R39A", ShortName = "39A", LongName = "UCD - Ongar", RouteType = 3 },
            ["RDART"] = new RouteInfo { RouteId = "RDART", ShortName = "DART", LongName = "Howth - Greystones", RouteType = 2 }
        };
    }

    [Fact]
    public void BuildMapVehicles_UsesShortAndLongRouteNames()
    {
        List<VehicleSnapshot> vehicles = new List<VehicleSnapshot>
        {
            new VehicleSnapshot { VehicleId = "V1", RouteId = "R39A", Latitude = 53.35, Longitude = -6.26, PositionTime = 1000 }
        };

        List<MapVehicle> result = BusLogic.BuildMapVehicles(vehicles, SampleRoutes());

        Assert.Single(result);
        Assert.Equal("39A", result[0].RouteName);
        Assert.Equal("UCD - Ongar", result[0].RouteLongName);
        Assert.Equal(1000, result[0].PositionTime);
    }

    [Fact]
    public void BuildMapVehicles_SkipsBadPositions_AndNonBusRoutes()
    {
        List<VehicleSnapshot> vehicles = new List<VehicleSnapshot>
        {
            new VehicleSnapshot { VehicleId = "zero", RouteId = "R39A", Latitude = 0, Longitude = 0 },        // feed error
            new VehicleSnapshot { VehicleId = "bad", RouteId = "R39A", Latitude = 95, Longitude = -6.26 },     // impossible latitude
            new VehicleSnapshot { VehicleId = "train", RouteId = "RDART", Latitude = 53.35, Longitude = -6.2 },// rail, not bus
            new VehicleSnapshot { VehicleId = "bus", RouteId = "R39A", Latitude = 53.35, Longitude = -6.26 }
        };

        List<MapVehicle> result = BusLogic.BuildMapVehicles(vehicles, SampleRoutes());

        Assert.Single(result);
        Assert.Equal("bus", result[0].VehicleId);
    }

    [Fact]
    public void BuildMapVehicles_KeepsUnknownRoutes_WithTheRawId()
    {
        List<VehicleSnapshot> vehicles = new List<VehicleSnapshot>
        {
            new VehicleSnapshot { VehicleId = "V1", RouteId = "NOT_IN_FILE", Latitude = 53.35, Longitude = -6.26 },
            new VehicleSnapshot { VehicleId = "V2", RouteId = "", Latitude = 53.36, Longitude = -6.27 }
        };

        List<MapVehicle> result = BusLogic.BuildMapVehicles(vehicles, SampleRoutes());

        Assert.Equal(2, result.Count);
        Assert.Equal("NOT_IN_FILE", result[0].RouteName);
        Assert.Equal("Unknown route", result[1].RouteName);
        Assert.Equal("", result[0].RouteLongName);
    }
}
