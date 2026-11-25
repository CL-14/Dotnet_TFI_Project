namespace TfiBus.Core;

// Turns per-route, per-day rows into the totals the pages show, keeping buses only.
public static class ReliabilityCalculator
{
    // Per-day totals over bus routes. A route that is not in the schedule (or is not a bus) is ignored.
    public static List<DailyReliability> Daily(List<RouteDayReliability> rows, Dictionary<string, RouteInfo> routes)
    {
        Dictionary<string, DailyReliability> byDay = new Dictionary<string, DailyReliability>();

        foreach (RouteDayReliability row in rows)
        {
            if (!routes.TryGetValue(row.RouteId, out RouteInfo? info) || info.RouteType != 3)
            {
                continue;
            }

            DailyReliability? day;
            if (!byDay.TryGetValue(row.StartDate, out day))
            {
                day = new DailyReliability { StartDate = row.StartDate };
                byDay[row.StartDate] = day;
            }
            day.Predicted += row.Predicted;
            day.Cancelled += row.Cancelled;
            day.NeverSeen += row.NeverSeen;
            day.Silent += row.Silent;
        }

        List<DailyReliability> result = new List<DailyReliability>(byDay.Values);
        // Newest day first: compares the two dates as text (YYYYMMDD sorts correctly).
        result.Sort((a, b) => string.Compare(b.StartDate, a.StartDate, StringComparison.Ordinal));
        return result;
    }

    // Per-route totals over bus routes, worst first, ignoring routes with fewer than minTrips trips
    // (a handful of trips would make the percentage meaningless).
    public static List<RouteReliability> ByRoute(List<RouteDayReliability> rows, Dictionary<string, RouteInfo> routes, int minTrips)
    {
        Dictionary<string, RouteReliability> byRoute = new Dictionary<string, RouteReliability>();

        foreach (RouteDayReliability row in rows)
        {
            if (!routes.TryGetValue(row.RouteId, out RouteInfo? info) || info.RouteType != 3)
            {
                continue;
            }

            RouteReliability? entry;
            if (!byRoute.TryGetValue(row.RouteId, out entry))
            {
                entry = new RouteReliability { RouteId = row.RouteId, Name = (info.ShortName + " " + info.LongName).Trim() };
                byRoute[row.RouteId] = entry;
            }
            entry.Predicted += row.Predicted;
            entry.Cancelled += row.Cancelled;
            entry.NeverSeen += row.NeverSeen;
            entry.Silent += row.Silent;
        }

        List<RouteReliability> result = new List<RouteReliability>();
        foreach (RouteReliability route in byRoute.Values)
        {
            if (route.Predicted >= minTrips) { result.Add(route); }
        }
        result.Sort((a, b) => b.SilentPercent.CompareTo(a.SilentPercent));
        return result;
    }
}
