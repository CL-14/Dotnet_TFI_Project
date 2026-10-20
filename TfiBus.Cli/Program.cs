using TfiBus.Core;
using TransitRealtime;
using Stop = TfiBus.Core.Stop;


string path = Path.Combine(AppContext.BaseDirectory, "GTFS_Realtime", "stops.txt");

Dictionary<string, Stop> stops = BusLogic.BuildStopLookup(path);
Console.WriteLine($"Loaded {stops.Count} stops.");


// TryGetValue is the safe way to look up a key: it returns true/false instead
// of throwing, "out" hands back the found value.
if (stops.TryGetValue("8269", out Stop? found))
{
    Console.WriteLine(found.StopName);
}

try
{
    //returns null if the variable isn't set.
    string? apiKey = Environment.GetEnvironmentVariable("TFI_API_KEY");

    //created once and disposed at the end of the program
    using HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10)};

    FeedClient client = new FeedClient(http, apiKey ?? "");

    FeedMessage feed = await client.GetVehicleFeedAsync();

    Console.WriteLine($"Entities in feed: {feed.Entity.Count}"); //fixed the property name as it was using an old naming convention

}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Request failed: {ex.StatusCode} - {ex.Message}");

}
catch (TaskCanceledException)
{
    Console.WriteLine("Timed out.");
}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message);
}
