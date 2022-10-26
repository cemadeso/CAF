#define PARALLEL

using ProcessOpenStreetMap;
using RoadNetwork;
using System.Diagnostics;
using System.Threading;
using System.Timers;

//var rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Panama\Days";
//var rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Days";
var rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Days";
//var rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Rio\Days";
var year = 2019;
var month = 9;

Console.WriteLine("Loading road network...");
//Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Rio.osmx");
//Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Bogota\Bogota.osmx");
Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\BuenosAires.osmx");
//Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Panama\Panama.osmx");

void ProcessRoadtimes(string directoryName, int day)
{
    Console.WriteLine("Loading Chunks...");
    var allDevices = ChunkEntry.LoadOrderedChunks(directoryName);
    Console.WriteLine("Finished loading Entries...");
    int processedDevices = 0;
    int failedPaths = 0;
    Console.WriteLine("Starting to process entries.");
    var watch = Stopwatch.StartNew();
    var totalEntries = allDevices.Sum(dev => dev.Length);
    List<ProcessedRecord> processedRecords = new(totalEntries);
    Parallel.ForEach(Enumerable.Range(0, allDevices.Length),
        () =>
        {
            return (Cache: network.GetCache(), Results: new List<ProcessedRecord>(totalEntries / System.Environment.ProcessorCount));
        },
        (deviceIndex, _, local) =>
        {
            var device = allDevices[deviceIndex];
            var (cache, records) = (local.Cache, local.Results);
            int startingIndex = 0;
            float currentX = device[0].Lat, currentY = device[0].Long;

            void Process(int startingIndex, int currentIndex, float straightLineDistance)
            {
                var startingPoint = device[startingIndex];
                var entry = device[currentIndex];
                var (time, distance, originRoadType, destinationRoadType) = network.Compute(startingPoint.Lat, startingPoint.Long, entry.Lat,
                    entry.Long, cache.fastestPath, cache.dirtyBits);
                if (time < 0)
                {
                    Interlocked.Increment(ref failedPaths);
                }
                // check to see if we need to add an extra record for the final point before we move
                if(startingIndex != currentIndex)
                {
                    records.Add(new ProcessedRecord(deviceIndex, currentIndex - 1, 0, 0, 0, -1, HighwayType.NotRoad, HighwayType.NotRoad));
                }
                records.Add(new ProcessedRecord(deviceIndex, currentIndex, time, distance, straightLineDistance, currentIndex - startingIndex + 1
                    , originRoadType, destinationRoadType));
            }
            records.Add(new ProcessedRecord(deviceIndex, 0, float.NaN, float.NaN, float.NaN, 1, HighwayType.NotRoad, HighwayType.NotRoad));
            for (int i = 1; i < device.Length; i++)
            {
                const float distanceThreshold = 0.1f;
                var straightLineDistance = Network.ComputeDistance(currentX, currentY, device[i].Lat, device[i].Long);
                // If we are in a "new location" add an entry.
                if (straightLineDistance > distanceThreshold)
                {
                    Process(startingIndex, i, straightLineDistance);
                    startingIndex = i;
                    currentX = device[i].Lat;
                    currentY = device[i].Long;
                }
                else
                {
                    // If we are not then update the current X,Y
                    var entries = (float)(i - startingIndex + 1);
                    currentX = (currentX * (entries - 1) + device[i].Lat) / entries;
                    currentY = (currentY * (entries - 1) + device[i].Long) / entries;
                }
            }
            var p = Interlocked.Increment(ref processedDevices);
            if (p % 1000 == 0)
            {
                var ts = TimeSpan.FromMilliseconds(((float)watch.ElapsedMilliseconds / p) * (allDevices.Length - p));
                Console.Write($"Processing {p} of {allDevices.Length}, Estimated time remaining: " +
                    $"{(ts.Days != 0 ? ts.Days + ":" : "")}{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}\r");
            }

            return (cache, records);
        }
    , (local) =>
        {
            lock (processedRecords)
            {
                processedRecords.AddRange(local.Results);
            }
        }
    );
    watch.Stop();
    Console.WriteLine($"\n{failedPaths} were unable to be computed.");
    Console.WriteLine($"Total runtime for entries: {watch.ElapsedMilliseconds}ms");
    Console.WriteLine("Writing Records...");
    using var writer = new StreamWriter(Path.Combine(directoryName, $"ProcessedRoadTimes-Day{day}.csv"));
    writer.WriteLine("DeviceId,Lat,Long,hAccuracy,TS,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
    foreach (var deviceRecords in processedRecords
        .GroupBy(entry => entry.DeviceIndex, (id, deviceRecords) => (ID: id, Records: deviceRecords.OrderBy(record => record.PingIndex)))
        .OrderBy(dev => dev.ID)
        )
    {
        foreach (var entry in deviceRecords.Records)
        {
            writer.Write(allDevices[entry.DeviceIndex][entry.PingIndex].DeviceID);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.PingIndex].Lat);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.PingIndex].Long);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.PingIndex].HAccuracy);
            writer.Write(',');
            writer.Write(allDevices[entry.DeviceIndex][entry.PingIndex].TS);
            writer.Write(',');
            writer.Write(entry.TravelTime);
            writer.Write(',');
            writer.Write(entry.RoadDistance);
            writer.Write(',');
            writer.Write(entry.Distance);
            writer.Write(',');
            writer.Write(entry.Pings);
            writer.Write(',');
            writer.Write((int)entry.OriginRoadType);
            writer.Write(',');
            writer.WriteLine((int)entry.DestinationRoadType);
        }
    }
}

var numberOfDaysInMonth = DateTime.DaysInMonth(year, month);

for (int i = 1; i <= numberOfDaysInMonth; i++)
{
    var directory = Path.Combine(rootDirectory, $"Day{i}");
    Console.WriteLine($"Starting to process {directory}");
    ProcessRoadtimes(directory, i);
}

Console.WriteLine("Complete");

record ProcessedRecord(int DeviceIndex, int PingIndex, float TravelTime, float RoadDistance, float Distance, int Pings,
    HighwayType OriginRoadType, HighwayType DestinationRoadType);
