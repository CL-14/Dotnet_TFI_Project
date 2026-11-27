namespace TfiBus.Core;

// One bus as the live map needs it: where it is and what to write in its pop-up
// It is kept separate from VehicleSnapshot so the page only sends the browser the fields
// it actually uses, already joined with the route names from routes.txt.
public class MapVehicle
{
    public string VehicleId { get; set; } = "";
    public string RouteName { get; set; } = "";      // short name such as "39A", or the raw route id if unknown
    public string RouteLongName { get; set; } = "";  // e.g. "Burlington Road - Ongar". empty if unknown
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Unix time (seconds since 1970) when the bus reported this position, 0 if the feed left it out.
    public long PositionTime { get; set; }
}
