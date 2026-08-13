
using Pege.Test.Core;

var streamUrl = "http://185.250.180.248:8080/stream/_";

// Кол-во новых потребителей на каждом шаге приращения.
var consumerStep = 1;

// Интервал в секундах между шагами.
var interval = 1;

// Максимальное кол-во потребителей.
var limit = 7;

// Продолжительность теста в минутах при максимуме потребителей.
var limitDuration = 10;

var consumers = new List<ConsumerPretender>();


for (var i = 0; i < limit; i++)
{
    if (i % consumerStep == 0)
        await Task.Delay(TimeSpan.FromSeconds(interval));    

    var c = new ConsumerPretender(i, streamUrl);
    c.ConnectionEstablished += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Playing");
    c.ConnectionLost += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Interrupted: {e.Exception.Message}");
    c.ConnectionFailed += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Connection failed: {e.Exception.Message}");
    c.DataDelay += (sender, e) => Console.WriteLine($"[{DateTime.Now}] [{e.Id}] Data delay: {e.Delay}");
    consumers.Add(c);
}

await Task.Delay(TimeSpan.FromMinutes(limitDuration));