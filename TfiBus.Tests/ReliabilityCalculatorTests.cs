using TfiBus.Core;

namespace TfiBus.Tests;

public class ReliabilityCalculatorTests
{
    [Fact]
    public void Daily_KeepsOnlyBusRoutes_AndByRoute_AppliesTheMinimumTripCount()
    {
        Dictionary<string, RouteInfo> routes = new Dictionary<string, RouteInfo>
        {
            ["B1"] = new RouteInfo { RouteId = "B1", ShortName = "1", LongName = "Town - Village", RouteType = 3 },
            ["B2"] = new RouteInfo { RouteId = "B2", ShortName = "2", RouteType = 3 },
            ["TRAIN"] = new RouteInfo { RouteId = "TRAIN", RouteType = 2 }
        };
        List<RouteDayReliability> rows = new List<RouteDayReliability>
        {
            new RouteDayReliability { StartDate = "20261118", RouteId = "B1", Predicted = 100, Cancelled = 5, NeverSeen = 15, Silent = 10 },
            new RouteDayReliability { StartDate = "20261118", RouteId = "B2", Predicted = 10, NeverSeen = 5, Silent = 5 },
            new RouteDayReliability { StartDate = "20261118", RouteId = "TRAIN", Predicted = 50, NeverSeen = 50, Silent = 50 },    // not a bus
            new RouteDayReliability { StartDate = "20261118", RouteId = "UNKNOWN", Predicted = 20, NeverSeen = 20, Silent = 20 }   // not in the schedule
        };

        List<DailyReliability> daily = ReliabilityCalculator.Daily(rows, routes);
        Assert.Single(daily);
        Assert.Equal(110, daily[0].Predicted);   // 100 + 10, no train and no unknown route
        Assert.Equal(15, daily[0].Silent);

        List<RouteReliability> byRoute = ReliabilityCalculator.ByRoute(rows, routes, 50);
        Assert.Single(byRoute);                  // B2 has only 10 trips
        Assert.Equal("1 Town - Village", byRoute[0].Name);
    }

    [Fact]
    public void LoadRoutes_ReadsRouteTypeAndNames()
    {
        string path = Path.Combine(Path.GetTempPath(), $"routes-{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "route_id,agency_id,route_short_name,route_long_name,route_type\n03C_120_e_a,03C,120,Dublin - Edenderry,3\nLUAS_RED,LUAS,Red,Tallaght - Point,0\n");
        try
        {
            Dictionary<string, RouteInfo> routes = BusLogic.LoadRoutes(path);

            Assert.Equal(2, routes.Count);
            Assert.Equal(3, routes["03C_120_e_a"].RouteType);
            Assert.Equal("120", routes["03C_120_e_a"].ShortName);
            Assert.Equal(0, routes["LUAS_RED"].RouteType);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
