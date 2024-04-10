// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;

var beginDate = new DateTime(2024, 1, 1, 0, 0, 0);
var endDate = new DateTime(2024, 12, 31, 23, 0, 0);

var hours = Enumerable.Range(0, (int)(endDate - beginDate).TotalHours + 1).Select(d => beginDate.AddHours(d));

var random = new Random();

var cups = new List<string>();
do
{
    cups.Add($"ES{random.Next(1000, 10000)}{random.Next(10000000, 100000000)}{random.Next(10000000, 100000000)}{(char)random.Next('A', 'Z' + 1)}{(char)random.Next('A', 'Z' + 1)}{random.Next(0, 9)}{(char)random.Next('A', 'Z' + 1)}");
}
while (cups.Count < 100);

var coefficients = cups.SelectMany(c => hours.Select(h => new Coefficient
{
    TimeStamp = h,
    Cups = c,
    Value = random.NextDouble()
}));

var batches = new List<List<Coefficient>>();
var batchSize = 1000;
var count = 0;

do
{
    var batch = coefficients.Skip(count).Take(batchSize).ToList();
    if (batch.Any())
    {
        batches.Add(batch);
        count += batchSize;
    }
}
while (batches.SelectMany(b => b.Select(s => s)).Count() < coefficients.Count());

var watch = new Stopwatch();
watch.Start();

const string connectionUri = "mongodb+srv://tomas:Cantabria30011978@cluster0.rowja.azure.mongodb.net/?retryWrites=true&w=majority&appName=Cluster0";

var settings = MongoClientSettings.FromConnectionString(connectionUri);

// Set the ServerApi field of the settings object to set the version of the Stable API on the client
settings.ServerApi = new ServerApi(ServerApiVersion.V1);

// Create a new client and connect to the server
var client = new MongoClient(settings);
var collection = client.GetDatabase("Tempelhof").GetCollection<Coefficient>("FacilityCoefficients");
if (collection is not null)
{
    foreach (var batch in batches)
    {
        await collection.InsertManyAsync(batch);
        Console.WriteLine($"Elapsed time: {watch.ElapsedMilliseconds}");
    }
}

Console.WriteLine("Finished");

internal class Coefficient
{
    public required DateTime TimeStamp { get; set; }

    public required string Cups { get; set; }

    public required double Value { get; set; }
}