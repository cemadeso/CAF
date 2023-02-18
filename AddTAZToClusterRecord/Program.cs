using NetTopologySuite;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Relate;
using System.Text;

var shapeFilePath = args.Length != 0 ? args[0] : @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Shapefile\BuenosAires_zone_epsg4326.shp";
var tazName = args.Length != 0 ? args[1] : "ZAT";
//var tazName = args.Length != 0 ? args[1] : "Zona_PIMUS";
var recordsPath = args.Length != 0 ? args[2] : @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Days\ProcessedRoadTimes.csv";
var outputPath = args.Length != 0 ? args[3] : @"Z:\Groups\TMG\Research\2022\CAF\BuenosAires\Days\ProcessedRoadTimes-WithTAZ.csv";
(var zoneSystem, var taz) = ReadShapeFile(shapeFilePath, tazName);

IndexedPointInAreaLocator[] zoneSystemLocator = zoneSystem.Select(z => new IndexedPointInAreaLocator(z)).ToArray();

static (Polygon[], int[] tazNumber) ReadShapeFile(string path, string tazFieldName)
{
    var accGeo = new List<Polygon>();
    var accTaz = new List<int>();
    var reader = new ShapefileDataReader(path, NetTopologySuite.Geometries.GeometryFactory.Default);

    // Get the TAZ field index
    int tazFieldIndex = reader.GetOrdinal(tazFieldName);
    if (tazFieldIndex < 0)
    {
        throw new Exception($"The shape file did not have any column {tazFieldName} for the TAZ");
    }
    while (reader.Read())
    {
        var geo = reader.Geometry;

        if (geo is Polygon p)
        {
            accGeo.Add(p);
            accTaz.Add(reader.GetInt32(tazFieldIndex));
        }
    }
    return (accGeo.ToArray(), accTaz.ToArray());
}

int IndexOfCollision(float x, float y, IndexedPointInAreaLocator[] zoneSystem)
{
    // For some reason the map has lat and long inverted
    var point = new Coordinate(y, x);

    for (int i = 0; i < zoneSystem.Length; i++)
    {
        //if (NetTopologySuite.Algorithm.Locate.SimplePointInAreaLocator.ContainsPointInPolygon(point, zoneSystem[i]))
        var test = zoneSystemLocator?[i].Locate(point) ?? Location.Null;
        if (!test.HasFlag(Location.Exterior))
        {
            return i;
        }
    }
    return -1;
}

Console.WriteLine(zoneSystem.Length);

(string original, int zoneNumber) ProcessRecord(string line)
{
    var parts = line.Split(',');
    var x = float.Parse(parts[1]);
    var y = float.Parse(parts[2]);
    var index = IndexOfCollision(x, y, zoneSystemLocator);
    var zoneNumber = index >= 0 ? taz[index] : -1;
    return (line, zoneNumber);
}

// Load in each record
using var writer = new StreamWriter(outputPath,
        new FileStreamOptions()
        {
            BufferSize = 0x1024 * 0x1024 * 100,
            Access = FileAccess.Write,
            Options = FileOptions.SequentialScan,
            Mode = FileMode.Create,
            Share = FileShare.None
        }
    );
//writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType");
writer.WriteLine("DeviceId,Lat,Long,hAccuracy,StartTime,EndTime,TravelTime,RoadDistance,Distance,Pings,OriginRoadType,DestinationRoadType,TAZ");
long count = 0;

foreach (var entry in File.ReadLines(recordsPath)
    .Skip(1)
    .AsParallel()
    .AsOrdered()
    .Select(line => ProcessRecord(line)))
{
    writer.Write(entry.original);
    writer.Write(',');
    writer.WriteLine(entry.zoneNumber);
    count++;
    if (count % 10000 == 0)
    {
        Console.Write("\r");
        Console.Write(count);
    }
}
