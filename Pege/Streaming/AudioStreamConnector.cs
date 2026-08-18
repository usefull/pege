using Pege.Entities;
using Pege.Interfaces;
using Pege.Test.Core;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;

namespace Pege.Streaming
{
    /// <summary>
    /// Коннектор аудио-стрима и контроллера.
    /// </summary>
    internal class AudioStreamConnector : IConnector
    {
        private const int IcyMetaInterval = 131072;
        private static readonly byte[] EmptyMetaBlock = [0x00];

        /// <summary>
        /// Метод соединения стрима и контроллера.
        /// </summary>
        /// <param name="stream">Стрим.</param>
        /// <param name="httpRequest">HTTP-запрос.</param>
        /// <param name="httpResponse">HTTP-ответ.</param>
        /// <param name="cancellationToken">Токен остановки.</param>
        public async Task ConsumeAsync(IStream stream, HttpRequest httpRequest, HttpResponse httpResponse, IConfiguration config, CancellationToken cancellationToken)
        {
            // **************** FOR TEST **************** //
            var _consumerRate = config.GetSection("ConsumerRate").Get<int[]>();
            lock (_lock)
            {
                _consumerCount++;
                if (_meters.Count < 50)
                {
                    _meter = new Meter();
                    _meters.Add(_meter);
                }

                if (_consumerCount > _consumerRate[_measureStage] && _consumerCount >= 50)
                {
                    _measureStage++;
                    foreach (var meter in _meters)
                        meter.StartMeasuring(_consumerRate[_measureStage].ToString(), [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15)]);

                    _log.Information($"{_consumerRate[_measureStage]}: measuring epoch started");

                    if (_measureStage + 1 == _consumerRate.Length)
                    {
                        _log.Information("Waiting for measuring finished ...");
                        _meters[0].MeasuringFinished += _meter_MeasuringFinished;
                    }
                }
            }
            // **************** FOR TEST **************** //

            bool supportIcy = httpRequest.Headers.ContainsKey("Icy-MetaData")
               && httpRequest.Headers["Icy-MetaData"] == "1";

            string userAgent = httpRequest.Headers.UserAgent.ToString() ?? "";
            bool isBrowser = userAgent.Contains("Mozilla") ||
                             userAgent.Contains("Chrome") ||
                             userAgent.Contains("Safari") ||
                             userAgent.Contains("Edge");

            httpResponse.ContentType = stream.Status.ContentType;
            httpResponse.Headers.Append("Cache-Control", "no-cache, no-store");
            httpResponse.Headers.Append("Access-Control-Expose-Headers", "icy-metaint, icy-pub, icy-name");

            httpResponse.Headers.Append("icy-name", isBrowser
                ? Uri.EscapeDataString($"{stream.Status.Title} ||| {stream.Status.Country}")
                : Encoding.GetEncoding("ISO-8859-1").GetString(Encoding.UTF8.GetBytes($"{stream.Status.Title} ||| {stream.Status.Country}")));

            httpResponse.Headers.Append("icy-pub", "1");
            if (supportIcy)
                httpResponse.Headers.Append("icy-metaint", IcyMetaInterval.ToString());

            var (reader, sessionId) = stream.Subscribe();

            int bytesSentInCurrentInterval = 0;
            byte[]? currentMetadata = null;

            try
            {
                await httpResponse.Body.FlushAsync(cancellationToken);

                await foreach (var chunk in reader.ReadAllAsync(cancellationToken).Cast<AudioChunk>())
                {
                    byte[]? pendingMetadata = chunk.StreamMetadata;
                    if (!supportIcy)
                    {
                        // **************** FOR TEST **************** //
                        var ts = Stopwatch.GetTimestamp();
                        // **************** FOR TEST **************** //

                        await httpResponse.Body.WriteAsync(chunk.Data, cancellationToken);

                        // **************** FOR TEST **************** //
                        double valueMs = Stopwatch.GetElapsedTime(ts).TotalMilliseconds;
                        _meter?.Apply(valueMs);
                        // **************** FOR TEST **************** //

                        continue;
                    }

                    ReadOnlyMemory<byte> audioData = chunk.Data;

                    while (audioData.Length > 0)
                    {
                        int bytesLeftInInterval = IcyMetaInterval - bytesSentInCurrentInterval;
                        int bytesToWrite = Math.Min(audioData.Length, bytesLeftInInterval);

                        await httpResponse.Body.WriteAsync(audioData[..bytesToWrite], cancellationToken);

                        if (bytesSentInCurrentInterval + bytesToWrite == IcyMetaInterval)
                        {
                            if (pendingMetadata != null && !pendingMetadata.AsSpan().SequenceEqual(currentMetadata ?? ReadOnlySpan<byte>.Empty))
                            {
                                currentMetadata = pendingMetadata;
                                await httpResponse.Body.WriteAsync(currentMetadata, cancellationToken);
                            }
                            else
                            {
                                await httpResponse.Body.WriteAsync(EmptyMetaBlock, cancellationToken);
                            }

                            bytesSentInCurrentInterval = 0;
                            audioData = audioData[bytesToWrite..];
                        }
                        else
                        {
                            bytesSentInCurrentInterval += bytesToWrite;
                            audioData = audioData[bytesToWrite..];
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
            finally
            {
                stream.Unsubscribe(sessionId);
            }
        }

        // **************** FOR TEST **************** //
        private void _meter_MeasuringFinished(object? sender, EventArgs e)
        {
            _meters[0].MeasuringFinished -= _meter_MeasuringFinished;
            _ = Task.Run(async () =>
            {
                var result = new StringBuilder("Label\tPeriod\tCount\tAvg\tMedian\tJitter\tP99\tMax\tStdDev\n");
                _meters[0].ReportItems.Keys.Aggregate(result, (acc, label) =>
                {
                    _meters[0].ReportItems[label].Aggregate(result, (ac, ps) =>
                    {
                        long count = 0;
                        double avg = 0;
                        double max = 0;
                        double median = 0;
                        double jitter = 0;
                        double p99 = 0;
                        foreach (var meter in _meters)
                        {
                            if (meter.ReportItems.ContainsKey(label))
                            {
                                var item = meter.ReportItems[label].First(p => p.PeriodIndex == ps.PeriodIndex);
                                count += item.Count;
                                avg += item.Avg * item.Count;
                                median += item.Median * item.Count;
                                jitter += item.Jitter * item.Count;
                                p99 += item.P99 * item.Count;
                                if (max < item.Max) max = item.Max;
                            }
                            else
                            {
                                Console.WriteLine($"-{label}");
                            }
                        }
                        avg = avg / count;
                        median = median / count;
                        jitter = jitter / count;
                        p99 = p99 / count;

                        double std = 0;
                        foreach (var meter in _meters)
                        {
                            if (meter.ReportItems.ContainsKey(label))
                            {
                                var item = meter.ReportItems[label].First(p => p.PeriodIndex == ps.PeriodIndex);
                                std += item.Count * ((item.StdDev * item.StdDev) + Math.Pow(item.Avg - avg, 2));
                            }
                        }
                        std = std / count;
                        std = Math.Sqrt(std);
                        ac.AppendLine($"{label}\tp{ps.PeriodIndex}\t{count}\t{avg:F1}\t{median:F1}\t{jitter:F1}\t{p99:F1}\t{max:F1}\t{std:F1}");
                        return ac;
                    });
                    return acc;
                });

                await File.WriteAllTextAsync("conector.log", result.ToString());
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                    _log.Error($"Stream log saving error: {(t.Exception.InnerException == null ? t.Exception.Message : t.Exception.InnerException.Message)}");
                else
                    _log.Information("Connector log saved.");
            });
        }
        // **************** FOR TEST **************** //

        protected readonly Serilog.ILogger _log = Log.Logger.ForContext("Stream", $"[Connector]");

        // **************** FOR TEST **************** //
        private readonly static List<Meter> _meters = [];
        private readonly static object _lock = new();
        private Meter? _meter;
        private static int _consumerCount = 0;
        private static int _measureStage = 0;
        // **************** FOR TEST **************** //
    }
}