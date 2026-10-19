using TfiBus.Core;

string path = Path.Combine(AppContext.BaseDirectory, "GTFS_Realtime", "stops.txt");

Dictionary<string, Stop> stops = BusLogic.BuildStopLookup(path);
Console.WriteLine($"Loaded {stops.Count} stops.");


// TryGetValue is the safe way to look up a key: it returns true/false instead
// of throwing, "out" hands back the found value.
if (stops.TryGetValue("8269", out Stop? found))
{
    Console.WriteLine(found.StopName);
}
