namespace TfiBus.Core;

// Counts for one route on one day, straight from the database.
public class RouteDayReliability
{
    public string StartDate { get; set; } = "";
    public string RouteId { get; set; } = "";
    public int Predicted { get; set; }
    public int Cancelled { get; set; }
    public int NeverSeen { get; set; }
    public int Silent { get; set; }
}

// Totals for one route over several days.
public class RouteReliability
{
    public string RouteId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Predicted { get; set; }
    public int Cancelled { get; set; }
    public int NeverSeen { get; set; }
    public int Silent { get; set; }

    public double SilentPercent
    {
        get { return Predicted == 0 ? 0 : 100.0 * Silent / Predicted; }
    }
}
