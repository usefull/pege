
using Pege.Test.Core;

//var streamUrl = "http://185.250.180.248:8080/stream/_";
var streamUrl = "http://localhost:5088/stream/_";

// Кол-во новых потребителей на каждом шаге приращения.
var consumerStep = 50;

// Интервал в секундах между шагами.
var interval = 40;

// Максимальное кол-во потребителей.
var limit = 200;

// Продолжительность теста в минутах при максимуме потребителей.
var limitDuration = 2880;

var consumers = new List<ConsumerPretender>();

var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = int.MaxValue,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
};
var httpClient = new HttpClient(handler)
{
    Timeout = Timeout.InfiniteTimeSpan
};

for (var i = 0; i < limit; i++)
{
    if (i % consumerStep == 0 && i != 0)
        await Task.Delay(TimeSpan.FromSeconds(interval));    

    var c = new ConsumerPretender(i, streamUrl, httpClient);
    c.ConnectionEstablished += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Playing");
    c.ConnectionLost += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Interrupted: {e.Exception.Message}");
    c.ConnectionFailed += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Connection failed: {e.Exception.Message}");
    c.DataDelay += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Data delay: {e.Delay}");
    consumers.Add(c);
}

await Task.Delay(TimeSpan.FromMinutes(limitDuration));