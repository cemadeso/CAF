#define PARALLEL

using ProcessOpenStreetMap;
using System.Diagnostics;
using System.Threading;
using System.Timers;

Console.WriteLine("Loading road network...");
Network network = new(@"./Rio.osmx");

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
            records.Add(new ProcessedRecord(deviceIndex, 0, float.NaN, float.NaN, float.NaN));
            for (int i = 1; i < device.Length; i++)
            {
                var startingPoint = device[i - 1];
                var entry = device[i];
                var (time, distance) = network.Compute(startingPoint.Lat, startingPoint.Long, entry.Lat,
                    entry.Long, cache.fastestPath, cache.dirtyBits);
                if (time < 0)
                {
                    Interlocked.Increment(ref failedPaths);
                }
                records.Add(new ProcessedRecord(deviceIndex, i, time, distance,
                    Network.ComputeDistance(startingPoint.Lat, startingPoint.Long, entry.Lat, entry.Long)));
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
    writer.WriteLine("DeviceId,Lat,Long,hAccuracy,TS,TravelTime,RoadDistance,Distance");
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
            writer.WriteLine(entry.Distance);
        }
    }
}

var rootDirectory = @"Z:\Groups\TMG\Research\2022\CAF\Rio\Days";

for (int i = 1; i <= 1; i++)
{
    var directory = Path.Combine(rootDirectory, $"Day{i}");
    Console.WriteLine($"Starting to process {directory}");
    ProcessRoadtimes(directory, i);
}

Console.WriteLine("Complete");

record ProcessedRecord(int DeviceIndex, int PingIndex, float TravelTime, float RoadDistance, float Distance);