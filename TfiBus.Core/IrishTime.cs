namespace TfiBus.Core;

// Turns times into Irish local time (Europe/Dublin, which switches between GMT and IST)
// so every page shows the same clock people see on their phone and at the bus stop.
// Kept in one place so the Alerts page and the live map format times the same way.
public static class IrishTime
{
    // The time zone, looked up once. Some small Docker images have no time zone data;
    // in that case UTC is used instead of crashing, and times are labelled "UTC".
    public static readonly TimeZoneInfo Zone = FindDublinZone();

    // True when the fallback is in use, so callers can label times honestly.
    public static bool IsFallback => Zone == TimeZoneInfo.Utc;

    private static TimeZoneInfo FindDublinZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    // Formats a moment in Irish time with the given pattern, e.g. "HH:mm:ss" or "d MMM yyyy HH:mm".
    public static string Format(DateTimeOffset moment, string pattern)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(moment, Zone);
        string suffix = IsFallback ? " UTC" : "";
        return local.ToString(pattern) + suffix;
    }

    // Same, for a Unix timestamp (seconds since 1970 in UTC), the form the feeds and database use.
    public static string FormatUnix(long unixSeconds, string pattern)
    {
        return Format(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), pattern);
    }
}
