using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Serialization;
using CsvHelper;


namespace TfiBus.Core;

public static class BusLogic
{
    public static double HaversineFormula(double Lat1Rad, double Lon1Rad, double Lat2Rad, double Lon2Rad)
    {

        Lat1Rad = Lat1Rad * (Math.PI / 180);
        Lon1Rad = Lon1Rad * (Math.PI / 180);
        Lat2Rad = Lat2Rad * (Math.PI / 180);
        Lon2Rad = Lon2Rad * (Math.PI / 180);

        //calculate the difference, that is, subtracting the two latitudes and longitudes from each other.

        double dlon = Lon2Rad - Lon1Rad;
        double dlat = Lat2Rad - Lat1Rad;

        //calculate the harvesine formula
        double HFormula = Math.Pow(Math.Sin(dlat / 2), 2) + Math.Cos(Lat1Rad) * Math.Cos(Lat2Rad) * Math.Pow(Math.Sin(dlon / 2), 2);

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
        // Key is stop_code, matching the Python version.
        Dictionary<string, Stop> stops = new Dictionary<string, Stop>();

            try
            {
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
            }
            catch (IOException)
            {
                Console.WriteLine("The file 'stops.txt' was not found.");
            }

            return stops;
        }

    // Return every stop whose name contains the query text, it also ignores upper/lower case
    // List<Stop> is a resizable list of Stop objects
    public static List<Stop> SearchStopsByName(string query, Dictionary<string, Stop> stopDict)
    {
        List<Stop> results = new List<Stop>();

        // Guard clause: without this, an empty query would match EVERY stop,
        // because every string "contains" an empty string.
        if (string.IsNullOrWhiteSpace(query))
        {
            return results; // show an empty list
        }

        //remove blank spaces from the begining and the and, like " test " to "test"
        string cleanQuery = query.Trim();


        foreach (Stop stop in stopDict.Values)
        {
           // Check if the stop name contains the query string, ignoring case
            if (stop.StopName.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(stop); // Python's append
            }
        }

        return results;
    }


}
