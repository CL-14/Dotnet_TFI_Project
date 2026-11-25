namespace TfiBus.Core;

// One row of the static schedule's routes.txt.
public class RouteInfo
{
    public string RouteId { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string LongName { get; set; } = "";
    public int RouteType { get; set; }   // GTFS route types: 3 = bus, 2 = rail, 0 = tram
}
