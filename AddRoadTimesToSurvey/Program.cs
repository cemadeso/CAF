using RoadNetwork;

var networkFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\bogota.osmx";
var surveyFilePath = @"Z:\Groups\TMG\Research\2022\CAF\Bogota\Survey.csv";
Console.WriteLine("Loading Road Network...");
Network network = new(networkFilePath);

var records = File.ReadLines(surveyFilePath)
    .Skip(1)
    .Select(line => line.Split(','))
    .Select(parts => (OX : float.Parse(parts[5]), OY:float.Parse(parts[4]),
                      DX : float.Parse(parts[8]), DY: float.Parse(parts[7])))
    .ToArray();

float[] travelTimeResults = new float[records.Length];
float[] travelDistanceResults = new float[records.Length];

int processedRecords = 0;

Parallel.For(0, records.Length,
    () => network.GetCache(),
    (i, _, networkCache) =>
    {
        // Get origin
        var results = network.Compute(records[i].OX, records[i].OY, records[i].DX, records[i].DY, networkCache.fastestPath, networkCache.dirtyBits);
        travelTimeResults[i] = results.time;
        travelDistanceResults[i] = results.distance;
        var processed = Interlocked.Increment(ref processedRecords);
        if (processed % 10000 == 0)
        {
            Console.Write($"\rProcessed Record {processed} of {records.Length}");
        }
        return networkCache;
    },
    (cache) => { } // Do nothing
);

Console.WriteLine();

using var writer = new StreamWriter(@"Z:\Groups\TMG\Research\2022\CAF\Bogota\SurveyTravelTimes.csv");
writer.WriteLine("RoadTime,RoadDistance");
for (int i = 0;i < travelTimeResults.Length;i++)
{
    writer.Write(travelTimeResults[i]);
    writer.Write(',');
    writer.WriteLine(travelDistanceResults[i]);
}
