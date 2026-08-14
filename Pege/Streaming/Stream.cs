
using Microsoft.EntityFrameworkCore;
using Pege.Data;
using Pege.Entities;
using Pege.Interfaces;
using Serilog;
using System.Diagnostics;
using System.Threading.Channels;

namespace Pege.Streaming
{
    internal abstract class Stream<TStatus, TChunk>(TStatus status, IServiceProvider serviceProvider) : IStream
        where TStatus : StreamStatus, new()
        where TChunk : Chunk, new()
    {
        public void Start()
        {
            _task = Task.Run(async () => {
                try { await BroadcastCycleAsync(_cts.Token); }
                catch (OperationCanceledException) { }
                catch (Exception e) { _log.Error(e.Message); }
            }, _cts.Token);

            Status.Started = DateTime.UtcNow;
            Status.Stopped = null;
        }

        public StreamStatus Status { get; protected set; } = status;

        public TStatus CastedStatus => Status as TStatus ?? throw new InvalidCastException();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _cts.Cancel();
            try
            {
                lock (_lock)
                {
                    foreach (var session in _consumers)
                        session.Writer.TryComplete();

                    _consumers.Clear();
                }
                _task?.GetAwaiter().GetResult();
                _ = SetStreamStopped(Status.Id!);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _cts?.Dispose();
                _task?.Dispose();
            }
        }

        public (ChannelReader<Chunk> Reader, Guid SessionId) Subscribe()
        {
            var channel = CreateChannel();
            var sessionId = Guid.NewGuid();
            var session = new ConsumerSession(sessionId, channel.Writer, channel.Reader);

            lock (_lock)
            {
                _consumers.Add(session);

                _isMeasuring = false; // ДЛЯ ТЕСТА
            }

            //_log.Information($"Consumers: {_consumers.Count}");

            return (channel.Reader, sessionId);
        }

        public void Unsubscribe(Guid sessionId)
        {
            lock (_lock)
            {
                var session = _consumers.FirstOrDefault(s => s.Id == sessionId);
                if (session != null)
                {
                    session.Writer.TryComplete();
                    _consumers.Remove(session);
                }
            }
            //_log.Information($"Consumers: {_consumers.Count}");
        }

        //protected void BroadcastChunk(Chunk chunk)
        //{
        //    lock (_lock)
        //    {
        //        if (_consumers.Count == 0) return;

        //        foreach (var consumer in _consumers)
        //            consumer.Writer.TryWrite(chunk);
        //    }
        //}

        /// <summary>
        /// ДЛЯ ТЕСТА
        /// </summary>
        /// <param name="chunk"></param>
        protected void BroadcastChunk(Chunk chunk)
        {
            long currentTimestamp = Stopwatch.GetTimestamp();
            int currentConsumersCount;

            lock (_lock)
            {
                currentConsumersCount = _consumers.Count;
                if (currentConsumersCount > 0)
                {
                    foreach (var consumer in _consumers)
                        consumer.Writer.TryWrite(chunk);
                }
            }

            // Если клиентов нет — сбрасываем состояние и ничего не мерим
            if (currentConsumersCount == 0)
            {
                _lastBroadcastTimestamp = 0;
                _isMeasuring = false;
                return;
            }

            // Перезапуск окна измерений, если флаг был сброшен в Subscribe()
            if (!_isMeasuring)
            {
                _isMeasuring = true;
                _startMeasureTimestamp = currentTimestamp;
                _lastBroadcastTimestamp = currentTimestamp;
                _chunksSentInInterval = 0;
                _maxDelayInIntervalTicks = 0;
                return; // Первый чанк после коннекта берем за точку отсчета времени
            }

            // Проверяем, укладываемся ли мы в 10-секундное окно
            if (currentTimestamp - _startMeasureTimestamp <= MeasureDurationTicks)
            {
                // Измеряем ритмичность между чанками внутри окна
                long elapsedTicks = currentTimestamp - _lastBroadcastTimestamp;
                if (elapsedTicks > _maxDelayInIntervalTicks)
                {
                    _maxDelayInIntervalTicks = elapsedTicks;
                }
                _lastBroadcastTimestamp = currentTimestamp;
                _chunksSentInInterval++;
            }
            else
            {
                // 10 секунд истекли! Выводим отчет ровно ОДИН раз за раунд вещания текущего состава
                if (_chunksSentInInterval > 0)
                {
                    double maxDelayMs = (_maxDelayInIntervalTicks * 1000.0) / Stopwatch.Frequency;

                    _log.Information($"[PULSE] 10s Window Finished. Consumers: {currentConsumersCount} | Chunks generated: {_chunksSentInInterval} | Max jitter: {maxDelayMs:F1}ms");

                    // Зануляем счетчик чанков, чтобы лог не спамил каждую итерацию до прихода следующего клиента
                    _chunksSentInInterval = 0;
                }
            }
        }

        private async Task SetStreamStopped(string id)
        {            
            using var db = _serviceProvider.GetService<IDbContextFactory<DataContext>>()?.CreateDbContext();
            if (db == null) return;
            
            var now = DateTime.UtcNow;
            await db.Streams.Where(si => si.Id == id.ToLower().Trim())
                .ExecuteUpdateAsync(s => s.SetProperty(si => si.Stopped, si => now));
        }

        protected abstract Task BroadcastCycleAsync(CancellationToken cancellationToken);

        protected virtual Channel<Chunk> CreateChannel() => Channel.CreateBounded<Chunk>(new BoundedChannelOptions(30)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

        protected readonly Serilog.ILogger _log = Log.Logger.ForContext("Stream", $"[Stream:{status.Id}]");

        protected IServiceProvider _serviceProvider = serviceProvider;

        private Task? _task;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<ConsumerSession> _consumers = [];
        private readonly Lock _lock = new();

        // ТОЛЬКО ДЛЯ ТЕСТА ЗАМЕРА РИТМИЧНОСТИ ГЕНЕРАЦИИ ЧАНКОВ
        private bool _isMeasuring = false;
        private long _lastBroadcastTimestamp = 0;
        private long _maxDelayInIntervalTicks = 0;
        private int _chunksSentInInterval = 0;
        private long _startMeasureTimestamp = 0;
        private static readonly long MeasureDurationTicks = Stopwatch.Frequency * 10; // Строго 10 секунд
    }
}
