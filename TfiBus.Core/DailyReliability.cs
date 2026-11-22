namespace TfiBus.Core;

// Reliability figures for one day of bus trips.
public class DailyReliability
{
    public string StartDate { get; set; } = "";   // for example "20261118"
    public int Predicted { get; set; }            // trips the NTA predicted
    public int Cancelled { get; set; }            // marked Canceled by the NTA
    public int NeverSeen { get; set; }            // never reported by any tracked vehicle
    public int Silent { get; set; }               // never seen AND not marked cancelled

    // Share of predicted trips that were silent, as a percentage.
    public double SilentPercent
    {
        get { return Predicted == 0 ? 0 : 100.0 * Silent / Predicted; }
    }
}
