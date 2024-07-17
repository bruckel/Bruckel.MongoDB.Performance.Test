using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

var referenceDay = DateTime.Today.AddMonths(-1);

var beginDate = new DateTime(referenceDay.AddYears(-1).Year, referenceDay.AddMonths(1).Month, 1);
var endDate = new DateTime(referenceDay.Year, referenceDay.Month, DateTime.DaysInMonth(referenceDay.Year, referenceDay.Month));

var hours = Enumerable.Range(0, (int)(endDate - beginDate).TotalHours + 1).Select(d => beginDate.AddHours(d));

var random = new Random();

// Legacy: como crear CUPS de manera totalmente aleatoria *//
//var cups = new List<string>();
//do
//{
//    cups.Add($"ES{random.Next(1000, 10000)}{random.Next(10000000, 100000000)}{random.Next(10000000, 100000000)}{(char)random.Next('A', 'Z' + 1)}{(char)random.Next('A', 'Z' + 1)}{random.Next(0, 9)}{(char)random.Next('A', 'Z' + 1)}");
//}
//while (cups.Count < 100);

// Creamos un conjunto para almacenar los números generados
var cups = new HashSet<int>();
while (cups.Count < 100)
{
    int numero = random.Next(1, 101);
    if (!cups.Contains(numero)) cups.Add(numero);
}

var coefficients = cups.SelectMany(c => hours.Select(h => new CoefficientTimeStamp
{
    TimeStamp = h.ToUniversalTime(),
    Metadata = new CoefficientMetadata
    {
        IdPeriod = c
    },
    Value = random.NextDouble()
}));

var hours1 = hours.Count();
var temp1 = coefficients.Count();

var watch = new Stopwatch();
watch.Start();

var settings = MongoClientSettings.FromConnectionString("mongodb://root:bruckel@localhost:27778/?directConnection=true");
settings.ServerApi = new ServerApi(ServerApiVersion.V1);
settings.LinqProvider = LinqProvider.V3;

var client = new MongoClient(settings);
var database = client.GetDatabase("temp");
var collections = database.ListCollectionNames().ToList();

var hasCollection = collections.Any(l => l == "coefficients");
if (!hasCollection)
{
    var createCollectionOptions = new CreateCollectionOptions
    {
        TimeSeriesOptions = new TimeSeriesOptions(timeField: "TimeStamp", metaField: "Metadata", granularity: TimeSeriesGranularity.Hours)
    };

    database.CreateCollection("coefficients", createCollectionOptions);
    
    var newCollection = database.GetCollection<CoefficientTimeStamp>("coefficients");

    var indexModel = new CreateIndexModel<CoefficientTimeStamp>(keys: Builders<CoefficientTimeStamp>.IndexKeys.Ascending(i => i.TimeStamp));

    //* Prueba de concepto de óndices y TTL *//
    // var indexModel = new CreateIndexModel<CoefficientTimeStamp>(keys: Builders<CoefficientTimeStamp>.IndexKeys.Ascending(i => i.TimeStamp),
    // options: new CreateIndexOptions<CoefficientTimeStamp>
    // {
    //    ExpireAfter = TimeSpan.FromSeconds(0),
    //    PartialFilterExpression = Builders<CoefficientTimeStamp>.Filter.Eq(i => i.Metadata.IsExpired, true)
    // });

    newCollection.Indexes.CreateOne(indexModel);
}

var collection = database.GetCollection<CoefficientTimeStamp>("coefficients");
await collection.InsertManyAsync(coefficients, new InsertManyOptions { IsOrdered = false });

Console.WriteLine($"Insert Finish time: {watch.ElapsedMilliseconds}");
watch.Restart();

var pipeline = new[]
{
    new BsonDocument
    {
        {
            "$addFields", new BsonDocument
            {
                {
                    "TimeStamp", new BsonDocument
                    {
                        {
                            "$dateToParts", new BsonDocument
                            {
                                { "date", "$TimeStamp" },
                                { "timezone", "+01:00" }
                            }
                        }
                    }
                }
                
            }
        }
    },
    new BsonDocument
    {
        {
            "$group", new BsonDocument
            {
                {
                    "_id", new BsonDocument
                    {
                        { "year", "$TimeStamp.year" },
                        { "month", "$TimeStamp.month" }
                    }
                },
                { "Value", new BsonDocument { { "$sum", "$Value" } } }
            }
        }
    },
    new BsonDocument
    {
        {
            "$merge", new BsonDocument
            {
                { "into", "coefficientsMonth" },
                { "whenMatched", "replace" }
            }
        }
    }
};

var result = collection.Aggregate<BsonDocument>(pipeline).ToList();

Console.WriteLine($"Aggregate Finish time: {watch.ElapsedMilliseconds}");

watch.Restart();

//hasCollection = collections.Any(l => l == "coefficientsMonth");
//if (!hasCollection)
//{
//    database.CreateView<BsonDocument, BsonDocument>("coefficientsMonth", "coefficients", pipeline);
//}

var viewCollection = database.GetCollection<BsonDocument>("coefficientsMonth");
var values = viewCollection.Find(Builders<BsonDocument>.Filter.Empty).ToCursor();

Console.WriteLine($"View Find time: {watch.ElapsedMilliseconds}");

internal class CoefficientTimeStamp
{
    public required BsonDateTime TimeStamp { get; set; }

    public required CoefficientMetadata Metadata { get; set; }

    public required double Value { get; set; }
}

internal class CoefficientMetadata
{
    public required int IdPeriod { get; set; }
}