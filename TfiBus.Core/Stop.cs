
namespace TfiBus.Core;

// One row of stops.txt. Properties are typed slots for each column needed.
public class Stop
{
    public string StopId { get; set; } = "";
    public string StopCode { get; set; } = "";
    public string StopName { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
}
