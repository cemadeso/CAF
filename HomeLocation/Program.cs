/*

HomeLocation

The goal of this program is to run the DBScan algorithm against each device

High Level Algorithm:

  1. Read in each line
  2. Break inputs into devices
  3. Run each device through DBScan
  4. Find the highest valued cluster and report that as the household TAZ
  5. Store the TAZ for each household if one is found, -1 if no household TAZ was found.
 */

// Main parameters
using HomeLocation;
using System.Collections.Concurrent;

var processedWithTAZFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Panama\Days\ProcessedRoadTimes-WithTAZ.csv";
var outputFileLocation = @"Z:\Groups\TMG\Research\2022\CAF\Panama\Days\HomeZone.csv";

//const int HourOffset = -3; // BuenosAires
const int HourOffset = -5; // Bogota + Panama

// Main Variables
ConcurrentBag<(string DeviceId, double Lat, double Lon, int stays, int clusters)> completedRecords = new();
BlockingCollection<DeviceRecords> deviceRecords = new(Environment.ProcessorCount * 2);


int TSToHour(long ts)
{
    var seconds = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
    var hour = (seconds.Hour + HourOffset) % 24;
    return hour >= 0 ? hour : hour + 24;
}


// Setup a task to load in the devices from file
Console.WriteLine("Loading in the records and running DBScan.");

Task.Run(() =>
{
    try
    {
        string? previousDevice = null;
        List<Point> points = new();
        foreach (var line in File.ReadLines(processedWithTAZFilePath)
                                 .Skip(1))
        {
            var parts = line.Split(',');
            // make sure that this is a valid line
            if (parts.Length >= 13)
            {
                var deviceId = parts[0];
                if (deviceId != previousDevice)
                {
                    // check to see if we have a real device to emit
                    if (previousDevice is not null)
                    {
                        deviceRecords.Add(new DeviceRecords(previousDevice, points));
                        points = new();
                    }
                }
                // DeviceId	Lat	Long	hAccuracy	StartTime	EndTime	TravelTime	RoadDistance	Distance	Pings	OriginRoadType	DestinationRoadType	TAZ
                // 0        1   2       3           4           5       6           7               8           9       10              11                  12
                int taz = int.Parse(parts[12]);
                // Ignore stays that are outside of the zone system.
                if (taz < 0)
                {
                    continue;
                }
                double lon = double.Parse(parts[1]);
                double lat = double.Parse(parts[2]);
                long startTime = long.Parse(parts[4]);
                long endTime = long.Parse(parts[5]);
                int pings = int.Parse(parts[9]);
                points.Add(new Point(lon, lat, pings, IsNightTime(startTime), endTime - startTime));
                previousDevice = deviceId;
            }
        }
        // deal with the final point
        if (previousDevice is not null && points.Count > 0)
        {
            deviceRecords.Add(new DeviceRecords(previousDevice, points));
        }
    }
    finally
    {
        deviceRecords.CompleteAdding();
    }
});

bool IsNightTime(long startTime)
{
    int hour = TSToHour(startTime);
    return hour <= 6 || hour >= 19;
}

// Setup something to go through each processed device and run
// the DBSCAN algorithm on each device, allowing each one
// to be generated in parallel


Parallel.ForEach(deviceRecords.GetConsumingEnumerable(), (device) =>
{
    var homeLocation = DBSCAN.GetHouseholdZone(device.Points);
    completedRecords.Add((device.DeviceId, homeLocation.Lat, homeLocation.Lon,
        device.Points.Count, homeLocation.clusters));
});


/*foreach (var device in deviceRecords.GetConsumingEnumerable())
{
    var homeLocation = DBSCAN.GetHouseholdZone(device.Points);
    completedRecords.Add((device.DeviceId, homeLocation.Lat, homeLocation.Lon));
}*/

// Now that all of the records have finished write out the results to file
Console.WriteLine("Writing the results out to file.");
using var writer = new StreamWriter(outputFileLocation);
writer.WriteLine("DeviceId,Lat,Lon,Stays,Clusters");
foreach (var (DeviceId, Lat, Lon, Stays, Clusters) in from x in completedRecords
                                                      orderby x.DeviceId
                                                      select x)
{
    writer.Write(DeviceId);
    writer.Write(',');
    writer.Write(Lat);
    writer.Write(',');
    writer.Write(Lon);
    writer.Write(',');
    writer.Write(Stays);
    writer.Write(',');
    writer.WriteLine(Clusters);
}


record struct DeviceRecords(string DeviceId, List<Point> Points);
