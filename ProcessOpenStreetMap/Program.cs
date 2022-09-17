// See https://aka.ms/new-console-template for more information

using ProcessOpenStreetMap;
using System.Diagnostics;
using System.Threading;

Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Rio.osmx");

var startingPoint = ChunkEntry.EnumerateEntries(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Chunked-2019.09.02\Chunk-1.csv").First();
var allEntries = ChunkEntry.EnumerateEntries(@"Z:\Groups\TMG\Research\2022\CAF\Rio\Chunked-2019.09.02\Chunk-1.csv").ToArray();
int processed = 0;
var watch = Stopwatch.StartNew();
foreach(var entry in allEntries)
{
    var results = network.Compute(startingPoint.Lat, startingPoint.Long, entry.Lat, entry.Long);
    processed++;
    if (processed % 100 == 0)
    {
        var remainingTime = TimeSpan.FromMilliseconds((watch.ElapsedMilliseconds / processed) * (allEntries.Length - processed));
        Console.Write($"Processing {processed} of {allEntries.Length}, Estimated time remaining: {remainingTime}\r");
    }
};

Console.WriteLine();
