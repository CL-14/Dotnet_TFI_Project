using TfiBus.Core;
using TransitRealtime;

namespace TfiBus.Tests;

public class FeedMapperTests
{
    [Fact]
    public void MapVehicles_KeepsPositionsApart_AndSkipsEntitiesWithoutPosition()
    {
        FeedMessage feed = new FeedMessage();

        // A complete vehicle. The latitude (53.4) and longitude (-6.26) differ on purpose,
        // so swapping them would be caught, like the earlier longitude bug.
        FeedEntity good = new FeedEntity { Id = "E1" };
        good.Vehicle = new VehiclePosition
        {
            Trip = new TripDescriptor { TripId = "T1", RouteId = "R1", StartDate = "20261102", StartTime = "08:00:00" },
            Vehicle = new VehicleDescriptor { Id = "V1" },
            Position = new Position { Latitude = 53.4f, Longitude = -6.26f, Bearing = 90f },
            Timestamp = 1000
        };

        // A vehicle with no position: must be skipped.
        FeedEntity noPosition = new FeedEntity { Id = "E2" };
        noPosition.Vehicle = new VehiclePosition { Vehicle = new VehicleDescriptor { Id = "V2" } };

        feed.Entity.Add(good);
        feed.Entity.Add(noPosition);

        List<VehicleSnapshot> result = FeedMapper.MapVehicles(feed);

        Assert.Single(result);
        Assert.Equal("V1", result[0].VehicleId);
        Assert.Equal("T1", result[0].TripId);
        Assert.InRange(result[0].Latitude, 53.39, 53.41);
        Assert.InRange(result[0].Longitude, -6.27, -6.25);
        Assert.Equal(1000, result[0].PositionTime);
    }

    [Fact]
    public void MapTripUpdates_UsesFirstStopDelay_AndNullWhenThereAreNoStopUpdates()
    {
        FeedMessage feed = new FeedMessage();

        // A trip whose first stop is 187 s late and second stop 173 s late: 187 must be used.
        FeedEntity withDelay = new FeedEntity { Id = "E1" };
        withDelay.TripUpdate = new TripUpdate { Trip = new TripDescriptor { TripId = "T1", StartDate = "20261102" } };
        withDelay.TripUpdate.StopTimeUpdate.Add(new TripUpdate.Types.StopTimeUpdate
        { StopSequence = 18, Arrival = new TripUpdate.Types.StopTimeEvent { Delay = 187 } });
        withDelay.TripUpdate.StopTimeUpdate.Add(new TripUpdate.Types.StopTimeUpdate
        { StopSequence = 19, Arrival = new TripUpdate.Types.StopTimeEvent { Delay = 173 } });

        // A cancelled trip with no stop updates: no delay, and the relationship is kept.
        FeedEntity cancelled = new FeedEntity { Id = "E2" };
        cancelled.TripUpdate = new TripUpdate
        {
            Trip = new TripDescriptor
            {
                TripId = "T2",
                StartDate = "20261102",
                ScheduleRelationship = TripDescriptor.Types.ScheduleRelationship.Canceled
            }
        };

        feed.Entity.Add(withDelay);
        feed.Entity.Add(cancelled);

        List<PredictedTrip> result = FeedMapper.MapTripUpdates(feed);

        Assert.Equal(2, result.Count);
        Assert.Equal(187, result[0].DelaySeconds);
        Assert.Null(result[1].DelaySeconds);
        Assert.Equal("Canceled", result[1].ScheduleRelationship);
    }

    [Fact]
    public void MapAlerts_RemovesDuplicateRoutes_AndReadsTextAndPeriod()
    {
        FeedMessage feed = new FeedMessage();

        FeedEntity entity = new FeedEntity { Id = "A1" };
        Alert alert = new Alert();
        alert.HeaderText = new TranslatedString();
        alert.HeaderText.Translation.Add(new TranslatedString.Types.Translation { Text = "Diversion on Route E2" });
        // The same route listed twice (the real feed does this), plus one stop.
        alert.InformedEntity.Add(new EntitySelector { RouteId = "R1" });
        alert.InformedEntity.Add(new EntitySelector { RouteId = "R1" });
        alert.InformedEntity.Add(new EntitySelector { StopId = "S1" });
        alert.ActivePeriod.Add(new TimeRange { Start = 100, End = 200 });
        alert.SeverityLevel = Alert.Types.SeverityLevel.Warning;
        entity.Alert = alert;
        feed.Entity.Add(entity);

        List<AlertRecord> result = FeedMapper.MapAlerts(feed);

        Assert.Single(result);
        Assert.Equal("Diversion on Route E2", result[0].Header);
        Assert.Equal("R1", result[0].RouteIds);   // not "R1,R1"
        Assert.Equal("S1", result[0].StopIds);
        Assert.Equal(100, result[0].ActiveFrom);
        Assert.Equal(200, result[0].ActiveTo);
        Assert.Equal("Warning", result[0].Severity);
    }
}
