using System.Globalization;
using CsvHelper;
using TransitRealtime;
//removed unused imports


namespace TfiBus.Core;

public static class BusLogic
{
    public static double HaversineFormula(double lat1, double lon1, double lat2, double lon2)
    {

        double lat1Rad = lat1 * (Math.PI / 180);
        double lon1Rad = lon1 * (Math.PI / 180);
        double lat2Rad = lat2 * (Math.PI / 180);
        double lon2Rad = lon2 * (Math.PI / 180);

        //calculate the difference, that is, subtracting the two latitudes and longitudes from each other.

        double dlon = lon2Rad - lon1Rad;
        double dlat = lat2Rad - lat1Rad;

        //calculate the harvesine formula
        double HFormula = Math.Pow(Math.Sin(dlat / 2), 2) + Math.Cos(lat1Rad) * Math.Cos(lat2Rad) * Math.Pow(Math.Sin(dlon / 2), 2);

        //calculate the great circle distance in radians
        double GCircleDistance = 2 * Math.Atan2(Math.Sqrt(HFormula), Math.Sqrt(1 - HFormula));

        //set the earth radius in kms
        const double radius = 6371.0; //radious of eath in KMs

        //calculate the distance
        double distance = radius * GCircleDistance;

        return distance;
    }

        // Same method as before, now returning Stop objects keyed by stop_code.
        public static Dictionary<string, Stop> BuildStopLookup(string filePath)
        {
        // This initializes an empty dictionary
        Dictionary<string, Stop> stops = new Dictionary<string, Stop>();

            //removed try catch and writeline

                //this is a declaration to cleanup after the file had been read, similar to fclose in python. Basically closes when the block ends.
                using StreamReader reader = new StreamReader(filePath);
                    using CsvReader csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                csv.Read();//move to the first line of the file being read


                csv.ReadHeader();
                //read the first line as the column names
                //for example, ours looks like this:
                //stop_id,stop_code,stop_name,stop_desc, etc.

                while (csv.Read()) //this will be advandcing the rows in the stops.txt from TFI
                {
                        // Build one Stop per row. This is the "store the entire row" part
                        Stop stop = new Stop();
                        stop.StopId = csv.GetField<string>("stop_id") ?? "";
                        stop.StopCode = csv.GetField<string>("stop_code") ?? "";
                        stop.StopName = csv.GetField<string>("stop_name") ?? "";
                        stop.Lat = csv.GetField<double>("stop_lat");
                        stop.Lon = csv.GetField<double>("stop_lon");

                        if (stop.StopCode == "") { continue; } // skip rows with no code
                        stops[stop.StopCode] = stop; // store the Stop object keyed by stop_code
                }


            return stops;
        }

    // Return every stop whose name contains the query text, it also ignores upper/lower case
    // List<Stop> is a resizable list of Stop objects
    public static List<Stop> SearchStopsByName(string query, Dictionary<string, Stop> stopDict)
    {
        List<Stop> results = new List<Stop>(); // Initialize an empty list to hold the results

        // Guard clause: without this, an empty query would match EVERY stop,
        // because every string "contains" an empty string.
        if (string.IsNullOrWhiteSpace(query))
        {
            return results; // show an empty list
        }

        //remove blank spaces from the begining and the and, like " test " to "test"
        string cleanQuery = query.Trim();


        foreach (Stop stop in stopDict.Values) // Iterate over the values of the dictionary, which are Stop objects
        {
           // Check if the stop name contains the query string, ignoring case
            if (stop.StopName.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(stop); // Equivalent to Python's to append
            }
        }

        return results;
    }

    //this brings in the FeedMessage and FeedEntity, the classes the package generates from the feed definition
    public static List<NearbyVehicle> GetNearbyVehicles(
        string stopCode,
        double radiusKm,
        Dictionary<string, Stop> stopDict,
        FeedMessage feed)
    {
        //collects the vehicles that pass the distance check.
        List<NearbyVehicle> nearby = new List<NearbyVehicle>();

        //look up the stop by key, if found 'stop' holds it and the method can continue
        //if nothing is found, we return the empty list. This function translation does not print anything to the terminal anymore like my python equivalent.
        //whoever calls it, the CLI or web page, can manage how to show the user the results


        //visits each entity in the feed. Lie the vehicle position, stop, trip updates, etc
        if (!stopDict.TryGetValue(stopCode, out Stop? stop))
        {
            return nearby;
        }
        foreach (FeedEntity entity in feed.Entity)
        {
            //skip entities with no vehicles section or other possible null values
            if (entity.Vehicle == null || entity.Vehicle.Position == null)
            {
                continue; // skip to the next entity
            }

            //the feed stores coordinates as float; c# is smart so it converts them to double automatically
            double vehicleLat = entity.Vehicle.Position.Latitude;
            double vehicleLon = entity.Vehicle.Position.Longitude; //fixed double latitude conversion showing instead of longitude
            //distance in km between the stop and this vehicle, using the haversine formula
            double distanceKm = HaversineFormula(stop.Lat, stop.Lon,vehicleLat, vehicleLon);

            if (distanceKm <= radiusKm)
            {
                NearbyVehicle found = new NearbyVehicle();

                //entity.Vehicle is the position report, the inner .Vehicle is the descriptor that carries the vehicle's id. the ?s are stops the chain and gives null if the descriptor is missing

                //then ?? "" substitutes an empty string so vehicleId is never nukll.

                found.VehicleId = entity.Vehicle.Vehicle?.Id ?? "";
                found.DistanceKm = distanceKm;
                nearby.Add(found);
            }

        }
        return nearby;
    }


}
