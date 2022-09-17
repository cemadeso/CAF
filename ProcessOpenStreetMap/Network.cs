using Itinero;
using Itinero.Algorithms.Networks;
using Itinero.IO.Osm;
using Itinero.Osm.Vehicles;
using Itinero.Profiles;
using OsmSharp.Streams;

namespace ProcessOpenStreetMap;

internal sealed class Network
{
    private readonly RouterDb _routerDb;
    private readonly Router _router;
    private readonly IProfileInstance _car;

    public Network(string fileName)
    {
        var requestedFile = new FileInfo(fileName);

        if (!requestedFile.Exists)
        {
            throw new FileNotFoundException(fileName);
        }
        _routerDb = GetRouter(requestedFile);
        _router = new Router(_routerDb);
        _car = Itinero.Osm.Vehicles.Vehicle.Car.Fastest();
    }

    public (float time, float distance) Compute(float originX, float originY, float destinationX, float destinationY)
    {
        try
        {
            var origin = _router.Resolve(_car, originX, originY);
            var destination = _router.Resolve(_car, destinationX, destinationY);
            var route = _router.Calculate(_car, origin, destination);
            return (route.TotalTime, route.TotalDistance);
        }
        catch(Exception)
        {
            return (-1.0f, -1.0f);
        }
    }

    private static RouterDb GetRouter(FileInfo requestedFile)
    {
        if (HasExtension(requestedFile, ".db"))
        {
            return LoadSerializedNetwork(requestedFile.FullName);
        }
        else if (HasExtension(requestedFile, "pbf"))
        {
            // If we already have this cached
            if (FileExists(requestedFile, ".db"))
            {
                return LoadSerializedNetwork(requestedFile.FullName + ".db");
            }
            else
            {
                return GenerateRoutes(requestedFile);
            }
        }
        else if (HasExtension(requestedFile, ".osmx"))
        {
            // If we already have this cached
            if (FileExists(requestedFile, ".pbf.db"))
            {
                return LoadSerializedNetwork(requestedFile.FullName + ".pbf.db");
            }
            else
            {
                // if we don't have the routers cached see if we have the network converted
                var itermediateFile = new FileInfo(requestedFile.FullName + ".pbf");
                if (!itermediateFile.Exists)
                {
                    // If it was not already converted, convert it
                    ConvertXMLToPBF(requestedFile);
                }
                // Load the network and store a cached copy to disk
                return GenerateRoutes(itermediateFile);
            }
        }
        else
        {
            throw new Exception("Unknown file format!");
        }
    }

    /// <summary>
    /// Load in an already cached network
    /// </summary>
    /// <param name="fileName">The filename of the cached network.</param>
    /// <returns>The cached network.</returns>
    private static RouterDb LoadSerializedNetwork(string fileName)
    {
        using var stream = new FileInfo(fileName).OpenRead();
        return RouterDb.Deserialize(stream);
    }

    private static RouterDb GenerateRoutes(FileInfo requestedFile)
    {
        var routerDb = new RouterDb();
        // Read in the Open Street Map Data and then convert it into a route database
        using (var stream = requestedFile.OpenRead())
        {
            routerDb.LoadOsmData(stream, Itinero.Osm.Vehicles.Vehicle.Car);
        }
        routerDb.OptimizeNetwork();
        using (var stream = new FileInfo(requestedFile.FullName + ".db").Open(FileMode.Create))
        {
            routerDb.Serialize(stream);
        }
        return routerDb;
    }

    private static void ConvertXMLToPBF(FileInfo file)
    {
        using var inStream = file.OpenRead();
        using var outStream = new FileInfo(file.FullName + ".pbf").Open(FileMode.Create);
        var source = new XmlOsmStreamSource(inStream);
        var sink = new PBFOsmStreamTarget(outStream);
        sink.RegisterSource(source);
        sink.Pull();
        sink.Flush();
    }

    private static bool HasExtension(FileInfo file, string extension) => file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase);

    private static bool FileExists(FileInfo requestedFile, string extension) => File.Exists(requestedFile.FullName + extension);
}

