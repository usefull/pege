
using Pege.Test.Core;

var streamUrl = new[]{
    "http://185.250.180.248:8080/stream/_",
    "http://185.250.180.248:8080/stream/a",
    "http://185.250.180.248:8080/stream/hr"
};

// Кол-во новых потребителей на каждом шаге приращения.
var consumerStep = 10;

// Интервал в секундах между шагами.
var interval = 5;

// Максимальное кол-во потребителей.
var limit = 100;

// Продолжительность теста в минутах при максимуме потребителей.
var limitDuration = 10;

var consumers = new List<ConsumerPretender>();

var urlIndex = 0;
for (var i = 0; i < limit; i++)
{
    if (i % consumerStep == 0)
        await Task.Delay(TimeSpan.FromSeconds(interval));

    if (urlIndex >= streamUrl.Length)
        urlIndex = 0;

    var c = new ConsumerPretender(i, streamUrl[urlIndex]);
    c.ConnectionEstablished += (sender, e) => Console.WriteLine($"[{e.Id}] Playing");
    c.ConnectionLost += (sender, e) => Console.WriteLine($"[{e.Id}] Interrupted: {e.Exception.Message}");
    c.ConnectionFailed += (sender, e) => Console.WriteLine($"[{e.Id}] Connection failed: {e.Exception.Message}");
    consumers.Add(c);

    urlIndex++;
}

await Task.Delay(TimeSpan.FromMinutes(limitDuration));