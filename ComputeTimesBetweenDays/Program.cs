using RoadNetwork;
using System.Reflection.Metadata.Ecma335;



// Goal: We need to go through each day and stitch together each device's last trip to the next day's
// first trip.
Console.WriteLine("Initializing the road network...");
Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Bogota.osmx");
var rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days\ProcessedRoadTimes";
var year = 2019;
var month = 9;

var numberOfDaysInMonth = DateTime.DaysInMonth(year, month);

Dictionary<string, LastDeviceEntry> lastEntries = new();

// walk through each day
for (int date = 1; date <= numberOfDaysInMonth; date++)
{
    var readFile = new FileInfo(Path.Combine(rootDirectory, $"ProcessedRoadTimes-Day{date}.csv"));
    if(!readFile.Exists)
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
    ReadOnlySpan<char> previousDevice = String.Empty;
    string? line = reader.ReadLine();
    writer.WriteLine(line);

    static (string id, LastDeviceEntry entry) Parse(string line, int date)
    {
        var parts = line.Split(',');
        return (parts[0], new LastDeviceEntry(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]), date));
    }

    // Add device entries are sorted so we only need to process the final line for each device
    while((line = reader.ReadLine()) is not null)
    {
        var currentDevice = line.AsSpan(0, line.IndexOf(','));
        // check to see if we are reading a new device
        if (previousDevice.Length == 0 || !previousDevice.SequenceEqual(currentDevice))
        {
            // Check to see if we are reading in the first device
            if (previousLine is null)
            {
                previousDevice = currentDevice;
                continue;
            }
            else
            {
                // If we are looking at a real device (the id is not empty) then
                // we need to store that last line to our dictionary
                var x = Parse(previousLine, date);
                lastEntries[x.id] = x.entry;

                // If we have a new device we need to check to see if we have encountered this
                // device before.  If we have seen it then we need to create a new trip from
                // the previous location to this new location.
                
                //_ = lastEntries.TryGetValue(x.id, x.entry);
                previousDevice = currentDevice;
            }
        }
        else
        {
            // just emit the current line
            writer.WriteLine(line);
        }
        previousLine = line;
    }
        
}

internal record LastDeviceEntry(float Lat, float Lon, float TimeStamp, int Date);