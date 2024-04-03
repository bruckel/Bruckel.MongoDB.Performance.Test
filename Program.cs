// See https://aka.ms/new-console-template for more information

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

Console.WriteLine(coefficients.Count());

internal class Coefficient
{
    public required DateTime TimeStamp { get; set; } 
    
    public required string Cups { get; set; } 

    public required double Value { get; set; } 
}