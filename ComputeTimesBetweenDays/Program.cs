using RoadNetwork;

internal class Program
{
    private static void Main(string[] args)
    {
        // Goal: We need to go through each day and stitch together each device's last trip to the next day's
        // first trip.
        Console.WriteLine("Initializing the road network...");
        //Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Bogota\Bogota.osmx");
        //var rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\ProcessedRoadTimes";
        if(args.Length != 2)
        {
            Console.WriteLine("USAGE: [Network File Path] [ProcessedRoadTimeDirectory]");
            return;
        }
        Network network = new(args[0]);
        var rootDirectory = args[1];
        var year = 2019;
        var month = 9;

        var numberOfDaysInMonth = DateTime.DaysInMonth(year, month);

        Dictionary<string, LastDeviceEntry> lastEntries = new();

        (var cache, var dirty) = network.GetCache();

        // walk through each day
        for (int date = 1; date <= numberOfDaysInMonth; date++)
        {
            var readFile = new FileInfo(Path.Combine(rootDirectory, $"ProcessedRoadTimes-Day{date}.csv"));
            if (!readFile.Exists)
            {
                Console.WriteLine($"Unable to find a processed road time file: {Path.Combine(rootDirectory, $"ProcessedRoadTimes-Day{date}.csv")}");
                continue;
            }
            Console.WriteLine($"Processing Day {date}...");
            //DeviceId,Lat,Long,hAccuracy,TS,TravelTime,RoadDistance,Distance,Pings
            using var reader = readFile.OpenText();
            using var writer = new StreamWriter(Path.Combine(rootDirectory, $"ProcessedRoadTimes-Day{date}-Patched.csv"));
            // burn the header
            string? previousLine = null;
            ReadOnlySpan<char> previousDevice = string.Empty;
            // Store the previous header line
            string? line = reader.ReadLine();
            writer.WriteLine(line);

            static (string id, LastDeviceEntry entry) Parse(string line, int date)
            {
                //writer.WriteLine("DeviceId,Lat,Long,hAccuracy,TS,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
                var parts = line.Split(',');
                return (parts[0], new LastDeviceEntry(float.Parse(parts[1]), float.Parse(parts[2]), date));
            }

            static (string Id, float Lat, float Lon, float hAccuracy, float ts, int pings)
                ParseLine(string line)
            {
                //writer.WriteLine("DeviceId,Lat,Long,hAccuracy,TS,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
                var parts = line.Split(',');
                return (
                        parts[0], // id
                        float.Parse(parts[1]), // lat
                        float.Parse(parts[2]), // lon
                        float.Parse(parts[3]), // hAcuraccy
                        float.Parse(parts[4]), // ts
                        int.Parse(parts[8])
                    );
            }

            void UpdateLastEntry(string? previousLine)
            {
                if (previousLine is not null)
                {
                    // If we are looking at a real device (the id is not empty) then
                    // we need to store that last line to our dictionary
                    var x = Parse(previousLine, date);
                    lastEntries[x.id] = x.entry;
                }
            }

            // Add device entries are sorted so we only need to process the final line for each device
            while ((line = reader.ReadLine()) is not null)
            {
                var currentDevice = line.AsSpan(0, line.IndexOf(','));
                // check to see if we are reading a new device
                if (previousDevice.Length == 0 || !previousDevice.SequenceEqual(currentDevice))
                {
                    UpdateLastEntry(previousLine);
                    // If we have a new device we need to check to see if we have encountered this
                    // device before.  If we have seen it then we need to create a new trip from
                    // the previous location to this new location.
                    var current = ParseLine(line);
                    if (lastEntries.TryGetValue(current.Id, out var previousEntry))
                    {
                        var results = network.Compute(previousEntry.Lat, previousEntry.Lon, current.Lat, current.Lon, cache, dirty);
                        writer.Write(current.Id);
                        writer.Write(',');

                        writer.Write(current.Lat);
                        writer.Write(',');
                        writer.Write(current.Lon);
                        writer.Write(',');
                        writer.Write(current.hAccuracy);
                        writer.Write(',');
                        writer.Write(current.ts);
                        writer.Write(',');
                        writer.Write(results.time);
                        writer.Write(',');
                        writer.Write(results.distance);
                        writer.Write(',');
                        writer.Write(current.pings);
                        writer.Write(',');
                        writer.Write(results.originRoad);
                        writer.Write(',');
                        writer.WriteLine(results.destinationRoad);
                    }
                    else
                    {
                        // If we can't add information just store the line.
                        writer.WriteLine(line);
                    }
                    previousDevice = currentDevice;
                }
                else
                {
                    // just emit the current line
                    writer.WriteLine(line);
                }
                previousLine = line;
            }

            // Make sure to store the results from the final device
            UpdateLastEntry(previousLine);
        }
    }
}

internal record LastDeviceEntry(float Lat, float Lon, int Date);
