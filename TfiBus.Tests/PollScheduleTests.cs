using TfiBus.Core;

namespace TfiBus.Tests;

public class PollScheduleTests
{
    [Fact]
    public void FeedFor_AlternatesVehiclesAndTripUpdates()
    {
        Assert.Equal(FeedKind.Vehicles, PollSchedule.FeedFor(0));
        Assert.Equal(FeedKind.TripUpdates, PollSchedule.FeedFor(1));
        Assert.Equal(FeedKind.Vehicles, PollSchedule.FeedFor(2));
    }

    [Fact]
    public void FeedFor_Over60Polls_UsesEachFeedTheExpectedNumberOfTimes()
    {
        int vehicles = 0, tripUpdates = 0, alerts = 0;

        for (long poll = 0; poll < 60; poll++)
        {
            FeedKind feed = PollSchedule.FeedFor(poll);
            if (feed == FeedKind.Vehicles) { vehicles++; }
            else if (feed == FeedKind.TripUpdates) { tripUpdates++; }
            else { alerts++; }
        }

        Assert.Equal(30, vehicles);
        Assert.Equal(28, tripUpdates);
        Assert.Equal(2, alerts);   // polls 29 and 59
        Assert.Equal(FeedKind.Alerts, PollSchedule.FeedFor(29));
    }
}
