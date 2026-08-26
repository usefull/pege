
using Pege.Test.Core;

var streamUrl = "http://185.250.180.248:8080/stream/_";
//var streamUrl = "http://localhost:5088/stream/_";

int[] consumerRate = [0, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900, 2000, 2100, 2200, 2300, 2400, 2500];
int[] periods = [10, 15];

var meter = new Meter();

// Интервал в секундах между шагами.
var interval = periods.Sum() + 10;

// Продолжительность теста в минутах при максимуме потребителей.
var limitDuration = 60;

var consumers = new List<ConsumerPretender>();

var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = int.MaxValue,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    MaxResponseHeadersLength = 100, // 100 КБ
    MaxResponseDrainSize = 1024 * 1024, // 1 МБ
};
var httpClient = new HttpClient(handler)
{
    Timeout = Timeout.InfiniteTimeSpan
};

var measureStage = 0;

while (consumers.Count < consumerRate.Last())
{
    if (consumers.Count > 0)
        await Task.Delay(TimeSpan.FromSeconds(interval));

    measureStage++;

    do
    {
        var c = new ConsumerPretender(consumers.Count, streamUrl, httpClient);
        c.ConnectionEstablished += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Playing");
        c.ConnectionLost += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Interrupted: {e.Exception.Message}");
        c.ConnectionFailed += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Connection failed: {e.Exception.Message}");
        c.DataDelay += (sender, e) => meter.Apply(e.DelayMs);
        consumers.Add(c);
    }
    while (consumers.Count < consumerRate[measureStage]);

    meter.StartMeasuring(consumers.Count.ToString(), [.. periods.Select(p => TimeSpan.FromSeconds(p))]);
    Console.WriteLine($"{consumers.Count}: measuring epoch started");
    if (measureStage + 1 == consumerRate.Length)
    {
        Console.WriteLine("Waiting for measuring finished ...");
        meter.MeasuringFinished += Utils.MeasuringFinished;
    }
}

await Task.Delay(TimeSpan.FromMinutes(limitDuration));

internal static class Utils
{
    public static void MeasuringFinished(object? sender, EventArgs e)
    {
        (sender as Meter)?.MeasuringFinished -= MeasuringFinished;
        _ = File.WriteAllTextAsync("client.log", (sender as Meter)?.Report).ContinueWith(t =>
        {
            if (t.Exception != null)
                Console.WriteLine($"Client log saving error: {(t.Exception.InnerException == null ? t.Exception.Message : t.Exception.InnerException.Message)}");
            else
                Console.WriteLine("Client log saved.");
        });
    }
}