namespace TfiBus.Core;

//An enum is a fixed list of named choices; a feed can only be one of these three
public enum FeedKind
{
    Vehicles,
    TripUpdates,
    Alerts
}

public static class PollSchedule
{
    //Decides which feed to fetch on poll number 0, 1, 2, ...
    //One request per poll, so that the NTA limit of one request per minute is respected:
    //vehicles and trip updates alternate, and every 30th poll fetches alerts instead
    //(replacing a trip-updates slot)
    public static FeedKind FeedFor(long pollNumber)
    {
        if (pollNumber % 30 == 29) //% is the remainder after division: 29, 59, 89, ...
        {
            return FeedKind.Alerts;
        }
        if (pollNumber % 2 == 0)
        {
            return FeedKind.Vehicles;
        }

        return FeedKind.TripUpdates;
    }
}
