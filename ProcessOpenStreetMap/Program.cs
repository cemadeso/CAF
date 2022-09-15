// See https://aka.ms/new-console-template for more information

using ProcessOpenStreetMap;

Network network = new(@"Z:\Groups\TMG\Research\2022\CAF\Rio.osmx");

var results = network.Compute(0, 0, 1, 1);

Console.WriteLine($"Time = {results.time}, Distance = {results.distance}");