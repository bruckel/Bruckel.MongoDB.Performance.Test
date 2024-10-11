using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

// Obtener la zona horaria de Europa Central (Central European Time, CET), si es Canarias es (Western European Time, WET)
var zone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

// Crear un DateTime en la zona horaria de Europa Central
var referenceDay = new DateTimeOffset(DateTime.Today.AddMonths(-1), zone.GetUtcOffset(DateTime.Today.AddMonths(-1)));
var beginDate = new DateTime(referenceDay.AddYears(-1).Year, referenceDay.AddMonths(1).Month, 1);
var endDate = new DateTime(referenceDay.Year, referenceDay.Month, DateTime.DaysInMonth(referenceDay.Year, referenceDay.Month));

var quarters = Enumerable.Range(0, (int)(endDate - beginDate).TotalMinutes / 15).Select(d => beginDate.AddMinutes(d * 15));

var random = new Random();

// Legacy: como crear CUPS de manera totalmente aleatoria *//
//var cups = new List<string>();
//do
//{
//    cups.Add($"ES{random.Next(1000, 10000)}{random.Next(10000000, 100000000)}{random.Next(10000000, 100000000)}{(char)random.Next('A', 'Z' + 1)}{(char)random.Next('A', 'Z' + 1)}{random.Next(0, 9)}{(char)random.Next('A', 'Z' + 1)}");
//}
//while (cups.Count < 100);

var contractCount = 200;

// Creamos un conjunto para almacenar los números generados
var cups = new HashSet<int>();
while (cups.Count < contractCount)
{
    int numero = random.Next(1, contractCount + 1);
    if (!cups.Contains(numero)) cups.Add(numero);
}

var curves = cups.SelectMany(c => quarters.Select(h => new CurveTimeStamp
{
    TimeStamp = h.ToUniversalTime(),
    Metadata = new CurveMetadata
    {
        IdContract = c,
        Offset = new DateTimeOffset(h).Offset.TotalMinutes
    },
    Value = random.NextDouble()
}));

//var settings = MongoClientSettings.FromConnectionString("mongodb+srv://contnet2:tentnoc2@inergycluster.vqqdg.mongodb.net/");
var settings = MongoClientSettings.FromConnectionString("mongodb://root:bruckel@localhost:27778/?directConnection=true");

settings.ServerApi = new ServerApi(ServerApiVersion.V1);
settings.LinqProvider = LinqProvider.V3;

var client = new MongoClient(settings);
var database = client.GetDatabase("temp");
var collection = database.GetCollection<CurveTimeStamp>("curves");

var options = new ListCollectionNamesOptions
{
    Filter = new BsonDocument("name", "curves")
};

if (!database.ListCollectionNames(options).Any())
{
    var createCollectionOptions = new CreateCollectionOptions
    {
        TimeSeriesOptions = new TimeSeriesOptions(timeField: "TimeStamp", metaField: "Metadata", granularity: TimeSeriesGranularity.Minutes)
    };
    database.CreateCollection("curves", createCollectionOptions);
    
    collection = database.GetCollection<CurveTimeStamp>("curves");
    collection.Indexes.CreateOne(new CreateIndexModel<CurveTimeStamp>(keys: Builders<CurveTimeStamp>.IndexKeys.Ascending(i => i.TimeStamp)));

    //* Prueba de concepto de índices y TTL. Esto nos sirve para especificar caducidad de series para las colecciones claculadas como los precios *//
    // var indexModel = new CreateIndexModel<CoefficientTimeStamp>(keys: Builders<CoefficientTimeStamp>.IndexKeys.Ascending(i => i.TimeStamp),
    // options: new CreateIndexOptions<CoefficientTimeStamp>
    // {
    //    ExpireAfter = TimeSpan.FromSeconds(0),
    //    PartialFilterExpression = Builders<CoefficientTimeStamp>.Filter.Eq(i => i.Metadata.IsExpired, true)
    // });    
}

var watch = new Stopwatch();
watch.Start();

collection = database.GetCollection<CurveTimeStamp>("curves");
await collection.InsertManyAsync(curves, new InsertManyOptions { IsOrdered = false });

Console.WriteLine($"Insert Finish time: {watch.ElapsedMilliseconds}");
watch.Restart();

var pipelineH = new[]
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
                        { "month", "$TimeStamp.month" },
                        { "day", "$TimeStamp.day" },
                        { "hour", "$TimeStamp.hour" }
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
                { "into", "curvesHour" },
                { "whenMatched", "replace" }
            }
        }
    }
};

collection.Aggregate<BsonDocument>(pipelineH);

watch.Restart();
var hours = database.GetCollection<BsonDocument>("curvesHour").Find(new BsonDocument()).ToList();
Console.WriteLine($"curvesHour Read Finish time: {watch.ElapsedMilliseconds}");
watch.Restart();

var pipelineD = new[]
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
                        { "month", "$TimeStamp.month" },
                        { "day", "$TimeStamp.day" }
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
                { "into", "curvesDay" },
                { "whenMatched", "replace" }
            }
        }
    }
};

collection.Aggregate<BsonDocument>(pipelineD);

watch.Restart();
var days = database.GetCollection<BsonDocument>("curvesDay").Find(new BsonDocument()).ToList();
Console.WriteLine($"curvesDay Read Finish time: {watch.ElapsedMilliseconds}");
watch.Restart();

var pipelineM = new[]
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
                { "into", "curvesMonth" },
                { "whenMatched", "replace" }
            }
        }
    }
};

collection.Aggregate<BsonDocument>(pipelineM);

watch.Restart();
var months = database.GetCollection<BsonDocument>("curvesMonth").Find(new BsonDocument()).ToList();
Console.WriteLine($"curvesMonth Read Finish time: {watch.ElapsedMilliseconds}");
watch.Restart();

var values = await collection.FindAsync(Builders<CurveTimeStamp>.Filter.Empty);
var items = values.ToList().Select(v => v.Value);

Console.WriteLine($"Total items: {items.Count()}");

watch.Stop();
Console.WriteLine($"Read Finish time: {watch.ElapsedMilliseconds}");

internal class CurveTimeStamp
{
    public required BsonDateTime TimeStamp { get; set; }

    public required CurveMetadata Metadata { get; set; }

    public required double Value { get; set; }
}

internal class CurveMetadata
{
    public required int IdContract { get; set; }
    public required double Offset { get; set; }
}