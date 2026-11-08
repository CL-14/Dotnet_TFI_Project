using TransitRealtime;

namespace TfiBus.Core;

public static class FeedMapper
{
    //converts the vehicles feed into a list of current vehicle positions.
    public static List<VehicleSnapshot> MapVehicles(FeedMessage feed)
    {
        List<VehicleSnapshot> result = new List<VehicleSnapshot>();

        foreach (FeedEntity entity in feed.Entity)
        {
            VehiclePosition? vehicle = entity.Vehicle;

            //skip entities without a vehicle section, a position or a descriptor, they cannot be placed on a map.
            if (vehicle == null || vehicle.Position == null || vehicle.Vehicle == null)
            {
                continue;
            }

            //the vehicle id is the primary key of vehicle_latest, so an empty one cannot be used.

            if (vehicle.Vehicle.Id == "")
            {
                continue;
            }

            VehicleSnapshot snapshot = new VehicleSnapshot();

            snapshot.VehicleId = vehicle.Vehicle.Id;
            snapshot.TripId = vehicle.Trip?.TripId ?? "";
            snapshot.RouteId = vehicle.Trip?.RouteId ?? "";
            snapshot.StartDate = vehicle.Trip?.StartDate ?? "";
            snapshot.StartTime = vehicle.Trip?.StartTime ?? "";
            snapshot.Latitude = vehicle.Position.Latitude;
            snapshot.Longitude = vehicle.Position.Longitude;
            snapshot.Bearing = vehicle.Position.Bearing;
            snapshot.PositionTime = (long)vehicle.Timestamp;

            result.Add(snapshot);

        }

        return result;
    }

    //convert the trip-updates feed into a list of predicted trips with their current delay
    public static List<PredictedTrip> MapTripUpdates(FeedMessage feed)
    {
        List<PredictedTrip> result = new List<PredictedTrip>();

        foreach(FeedEntity entity in feed.Entity)
        {
            TripUpdate? tripUpdate = entity.TripUpdate;

            //a prediction withou a trip id cannot be matched, so this is to skip it
            if (tripUpdate == null || tripUpdate.Trip == null || tripUpdate.Trip.TripId == "")
            {
                continue;
            }

            PredictedTrip predicted = new PredictedTrip();
            predicted.StartDate = tripUpdate.Trip.StartDate;
            predicted.TripId = tripUpdate.Trip.TripId;
            predicted.RouteId = tripUpdate.Trip.RouteId;
            predicted.StartTime = tripUpdate.Trip.StartTime;
            // The relationship is an enum (a fixed list of named values); ToString() turns it into text.
            predicted.ScheduleRelationship = tripUpdate.Trip.ScheduleRelationship.ToString();
            predicted.DelaySeconds = FirstStopDelay(tripUpdate);

            result.Add(predicted);

        }

        return result;
    }

    //convert the alert feed into a list of active alerts
    public static List<AlertRecord> MapAlerts(FeedMessage feed)
    {
        List<AlertRecord> result = new List<AlertRecord>();

        foreach (FeedEntity entity in feed.Entity)
        {
            Alert? alert = entity.Alert;
            if (alert == null)
            {
                continue;
            }

            // A HashSet stores each value only once. The feed repeats route ids
            // (the same route can be listed twice), and this removes the repeats.
            HashSet<string> routeIds = new HashSet<string>();
            HashSet<string> stopIds = new HashSet<string>();
            foreach (EntitySelector selector in alert.InformedEntity)
            {
                if (selector.RouteId != "") { routeIds.Add(selector.RouteId); }
                if (selector.StopId != "") { stopIds.Add(selector.StopId); }
            }

            AlertRecord record = new AlertRecord();
            record.AlertId = entity.Id;
            record.Header = FirstText(alert.HeaderText);
            record.Description = FirstText(alert.DescriptionText);
            // string.Join glues the values together with a comma between each.
            record.RouteIds = string.Join(",", routeIds);
            record.StopIds = string.Join(",", stopIds);
            record.Severity = alert.SeverityLevel.ToString();

            // An alert can have several active periods; the first is stored.
            if (alert.ActivePeriod.Count > 0)
            {
                TimeRange period = alert.ActivePeriod[0];
                record.ActiveFrom = (long)period.Start;
                record.ActiveTo = (long)period.End;
            }

            result.Add(record);
        }

        return result;
    }

    //return the delay at the first stop update of the trip, which is the bus's current lateness
    //returns null if the trip has no stop updates.
    private static int? FirstStopDelay(TripUpdate tripUpdate)
    {
        if (tripUpdate.StopTimeUpdate.Count == 0)
        {
            return null;
        }

        //nested feed types live under ".types" so he full name is needed here
        TripUpdate.Types.StopTimeUpdate first = tripUpdate.StopTimeUpdate[0];

        //prefer the arrival delay, fall back to the departure delay if there is no arrival
        if (first.Arrival != null) {return first.Arrival.Delay;}
        if (first.Departure != null){return first.Departure.Delay;}

        return null;
    }

    //alert text come as list of translation, take the first one, or "" if there is none
    private static string FirstText(TranslatedString? text)
    {
        if (text == null || text.Translation.Count == 0)
        {
            return "";
        }
        return text.Translation[0].Text;
    }
}
