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
    var allDevices = ChunkEntry.EnumerateEntries(directoryName)
        .GroupBy(chunk => chunk.DeviceID, (_, chunkGroup) => chunkGroup.OrderBy(c2 => c2.TS).ToArray())
        .ToArray();
    Console.WriteLine("Finished loading Entries...");
    int processedDevices = 0;
    int failedPaths = 0;
    Console.WriteLine("Starting to process entries.");
    var watch = Stopwatch.StartNew();
    var totalEntries = allDevices.Sum(dev => dev.Length);
    List<ProcessedRecord> processedRecords = new(totalEntries);
    Parallel.ForEach(allDevices,
        () =>
        {
            return (Cache: network.GetCache(), Results: new List<ProcessedRecord>(totalEntries / System.Environment.ProcessorCount));
        },
        (device, _, local) =>
        {
            var (cache, records) = (local.Cache, local.Results);
            records.Add(new ProcessedRecord(device[0].DeviceID, device[0].Lat, device[0].Long,
                device[0].HAccuracy, device[0].TS, float.NaN, float.NaN, float.NaN));
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
                records.Add(new ProcessedRecord(entry.DeviceID, entry.Lat, entry.Long, entry.HAccuracy, entry.TS, time, distance,
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
    foreach (var device in processedRecords
        .GroupBy(entry => entry.DeviceId, (id, deviceRecords) => (ID: id, Records: deviceRecords.OrderBy(record => record.TS)))
        .OrderBy(dev => dev.ID)
        )
    {
        var id = device.ID;
        foreach (var entry in device.Records)
        {
            writer.Write(id);
            writer.Write(',');
            writer.Write(entry.Lat);
            writer.Write(',');
            writer.Write(entry.Long);
            writer.Write(',');
            writer.Write(entry.HAccuracy);
            writer.Write(',');
            writer.Write(entry.TS);
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

for (int i = 1; i <= 30; i++)
{
    var directory = Path.Combine(rootDirectory, $"Day{i}");
    Console.WriteLine($"Starting to process {directory}");
    ProcessRoadtimes(directory, i);
}

Console.WriteLine("Complete");

record ProcessedRecord(string DeviceId, float Lat, float Long, float HAccuracy, float TS, float TravelTime, float RoadDistance, float Distance);