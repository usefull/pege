using Pege.Interfaces;
using Pege.Test.Core;
using System.Text;

namespace Pege.Streaming
{
    internal partial class AudioStreamConnector : IConnector
    {
        private void StartMeasurement(IConfiguration config)
        {
            var consumerRate = config.GetSection("ConsumerRate").Get<int[]>();
            var periods = config.GetSection("MeasuringPeriods").Get<int[]>();

            lock (_lock)
            {
                _consumerCount++;
                if (_meters.Count < 50)
                {
                    _meter = new Meter();
                    _meters.Add(_meter);
                }

                if (_consumerCount > consumerRate[_measureStage] && _consumerCount >= 50 && _measureStage < consumerRate.Length - 1)
                {
                    _measureStage++;
                    foreach (var meter in _meters)
                        meter.StartMeasuring(consumerRate[_measureStage].ToString(), [.. periods.Select(p => TimeSpan.FromSeconds(p))]);

                    _log.Information($"{consumerRate[_measureStage]}: measuring epoch started");

                    if (_measureStage + 1 == consumerRate.Length)
                    {
                        _log.Information("Waiting for measuring finished ...");
                        _meters[0].MeasuringFinished += MeasuringFinished;
                    }
                }
            }
        }

        private void MeasuringFinished(object? sender, EventArgs e)
        {
            _meters[0].MeasuringFinished -= MeasuringFinished;
            _ = Task.Run(async () =>
        {
                var result = new StringBuilder("Label\tPeriod\tCount\tAvg\tMedian\tJitter\tP99\tMax\tStdDev\n");
                var m = _meters.First(i => i.ReportItems.Count != 0);
                result = m.ReportItems.Keys.Aggregate(result, (acc, label) =>
                {
                    acc = m.ReportItems[label].Aggregate(result, (ac, ps) =>
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
                                var item = meter.ReportItems[label].FirstOrDefault(p => p.PeriodIndex == ps.PeriodIndex);
                                if (item != null)
                                {
                                    count += item.Count;
                                    avg += item.Avg * item.Count;
                                    median += item.Median * item.Count;
                                    jitter += item.Jitter * item.Count;
                                    p99 += item.P99 * item.Count;
                                    if (max < item.Max) max = item.Max;
                                }
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
                                var item = meter.ReportItems[label].FirstOrDefault(p => p.PeriodIndex == ps.PeriodIndex);
                                if (item != null)
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

                await File.WriteAllTextAsync("storage/connector.log", result.ToString());
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                    _log.Error($"Connector log saving error: {(t.Exception.InnerException == null ? t.Exception.Message : t.Exception.InnerException.Message)}");
                else
                    _log.Information("Connector log saved.");
            });
        }

        private Meter? _meter;
        private bool _delayMeasurementMode = false;

        private readonly static List<Meter> _meters = [];
        private readonly static object _lock = new();
        private static int _consumerCount = 0;
        private static int _measureStage = 0;
    }
}
