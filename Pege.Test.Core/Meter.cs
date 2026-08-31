using System.Diagnostics;
using System.Text;

namespace Pege.Test.Core
{
    public class Meter
    {
        private int _period = -1;
        private long _latestTimestamp;

        // Метрики (все считаются в миллисекундах как double для точности)
        private double _avg;
        private long _count;
        private double _max;
        private double _m2; // Для стандартного отклонения
        private double _lastValue = -1; // Для джиттера
        private double _jitter;

        private StreamingMedian? _median;
        private StreamingMedian? _p99; // 99-й перцентиль (худшие лаги)

        private readonly Dictionary<string, List<PeriodSummary>> _report = [];
        private readonly object _lock = new();

        private Dictionary<DateTime, string> _events;

        public event EventHandler? MeasuringFinished;

        public void StartMeasuring(string label, TimeSpan[] periods)
        {
            _ = Task.Run(async () =>
            {
                lock (_lock)
                {
                    if (!_report.ContainsKey(label))
                        _report.Add(label, []);
                }

                _period = 0;
                foreach (var p in periods)
                {
                    lock (_lock)
                    {
                        _latestTimestamp = 0;
                        _avg = 0;
                        _count = 0;
                        _max = 0;
                        _m2 = 0;
                        _lastValue = -1;
                        _jitter = 0;
                        _median = new StreamingMedian(0.5);  // Квантиль 0.5
                        _p99 = new StreamingMedian(0.99);    // Квантиль 0.99

                        _events = [];
                    }

                    await Task.Delay(p);

                    lock (_lock)
                    {
                        double stdDev = _count > 1 ? Math.Sqrt(_m2 / (_count - 1)) : 0;

                        var lna = _events.Where(e => e.Value.StartsWith("lna")).Select(e => e.Key).OrderByDescending(e => e);
                        var enc = _events.Where(e => e.Value.StartsWith("enc")).Select(e => e.Key).OrderByDescending(e => e);
                        var adts = _events.Where(e => e.Value.StartsWith("adts")).Select(e => e.Key).OrderByDescending(e => e);                        

                        _report[label].Add(new PeriodSummary
                        {
                            PeriodIndex = _period,
                            Count = _count,
                            Avg = _avg,
                            Median = _median.Value,
                            P99 = _p99.Value,
                            Max = _max,
                            Jitter = _jitter,
                            StdDev = stdDev,
                            LNA = lna.Any() ? lna.First() - lna.Last() : TimeSpan.Zero,
                            Encoding = enc.Any() ? enc.First() - enc.Last() : TimeSpan.Zero,
                            ADTS = adts.Any() ? adts.First() - adts.Last() : TimeSpan.Zero,
                        });
                    }

                    _period++;
                }

                _period = -1;
                MeasuringFinished?.Invoke(this, EventArgs.Empty);
            });
        }

        public Dictionary<string, List<PeriodSummary>> ReportItems => _report;

        public string Report
        {
            get
            {
                var result = new StringBuilder("Label\tPeriod\tCount\tAvg\tMedian\tJitter\tP99\tMax\tStdDev\tLNA\tEncode\tADTS\n");
                result = _report.Aggregate(result, (acc, i) =>
                {
                    acc = i.Value.Aggregate(acc, (ac, j) =>
                    {
                        ac.AppendLine($"{i.Key}\tp{j.PeriodIndex}\t{j.Count}\t{j.Avg:F1}\t{j.Median:F1}\t{j.Jitter:F1}\t{j.P99:F1}\t{j.Max:F1}\t{j.StdDev:F1}\t{j.LNA.TotalSeconds:F1}\t{j.Encoding.TotalSeconds:F1}\t{j.ADTS.TotalSeconds:F1}");
                        return ac;
                    });
                    return acc;
                });
                return result.ToString();
            }
        }

        public bool IsMeasuring => _period >= 0;

        public void Fire()
        {
            // Быстрая проверка без lock. Если замеры не идут или закончились — игнорируем
            if (_period < 0) return;

            var ts = Stopwatch.GetTimestamp();

            _ = Task.Run(() =>
            {
                lock (_lock)
                {
                    // Защита: если период завершился, пока таска ставилась в очередь
                    if (_period < 0) return;

                    var lts = _latestTimestamp;
                    _latestTimestamp = ts;

                    // Если это первый чанк в текущем периоде, у нас нет пары для вычисления интервала
                    if (lts == 0) return;

                    // Точный перевод разницы тиков Stopwatch в миллисекунды для любой ОС
                    double valueMs = Stopwatch.GetElapsedTime(lts, ts).TotalMilliseconds;

                    Update(valueMs);
                }
            });
        }

        public void Apply(double valueMs)
        {
            _ = Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_period < 0) return;

                    Update(valueMs);
                }
            });
        }

        private void Update(double valueMs)
        {
            //Console.WriteLine($"Chunk interval: {Math.Round(valueMs)} ms");

            _count++;

            // 1. Максимум
            if (valueMs > _max) _max = valueMs;

            // 2. Кумулятивное среднее
            double delta = valueMs - _avg;
            _avg += delta / _count;

            // 3. Стандартное отклонение (Алгоритм Велфорда)
            double delta2 = valueMs - _avg;
            _m2 += delta * delta2;

            // 4. Джиттер (Дрожание фазы по стандарту RTP RFC 3550)
            if (_lastValue >= 0)
            {
                double diff = Math.Abs(valueMs - _lastValue);
                _jitter += (diff - _jitter) / 16.0;
            }
            _lastValue = valueMs;

            // 5. Перцентили (Медиана и p99)
            _median!.Update(valueMs);
            _p99!.Update(valueMs);
        }

        public void Event(string e)
        {
            lock (_lock)
            {
                if (_period < 0) return;
                _events.Add(DateTime.Now, e);
            }
        }
    }
}
