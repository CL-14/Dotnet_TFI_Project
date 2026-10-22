using System.Runtime.CompilerServices;
using TfiBus.Core;
using TransitRealtime;
using Stop = TfiBus.Core.Stop;

namespace TfiBus.Tests;

public class BusLogicTests
{
    //this marks the method as a test for xUnit to run
    [Fact]
    public void GetNearbyVehicles_ReturnsOnlyVehiclesInsideRadius()
    {
        //one stop built by hand to test
        Stop stop = new Stop {StopCode = "TEST1", StopName = "Test stop", Lat= 53.40, Lon = -6.26};

        Dictionary<string, Stop> stops = new Dictionary<string, Stop>();
        stops["TEST1"] = stop;

        // an empty feed, then two vehicles: 1.1km north of the stop and 11km north.
        //the f after the lat and lon is needed since the feed uses floats, f converts them to it
        FeedMessage feed = new FeedMessage();
        feed.Entity.Add(MakeVehicle("near", 53.41f, -6.26f));
        feed.Entity.Add(MakeVehicle("far", 53.50f, -6.26f));

        //the one call being tested, with a 5km radius
        List<NearbyVehicle> result = BusLogic.GetNearbyVehicles("TEST1", 5.0, stops, feed);

        //exactly one vehicle came back, it's the correct one
        //and its distance is about 1.11km
        Assert.Single(result);
        Assert.Equal("near", result[0].VehicleId);
        Assert.InRange(result[0].DistanceKm, 1.0, 1.2);
    }

        //helper that builds one feed entity holding a vehicle with an id and a position
        //private static mean it will only be usable in this test class
        private static FeedEntity MakeVehicle(string id, float lat, float lon)
        {
            FeedEntity entity = new FeedEntity();
            entity.Id = id;
            entity.Vehicle = new VehiclePosition();
            entity.Vehicle.Vehicle = new VehicleDescriptor {Id = id};
            entity.Vehicle.Position = new Position { Latitude = lat, Longitude = lon};

            return entity;

        }
 }

