using Microsoft.EntityFrameworkCore;
using Pege.Data;
using Pege.Entities;
using Pege.Interfaces;
using Serilog;
using System.Threading.Channels;

namespace Pege.Streaming
{
    internal abstract class Stream<TInfo, TStatus, TChunk> : IStream
        where TInfo : StreamInfo, new()
        where TStatus : StreamStatus, new()
        where TChunk : Chunk, new()
    {
        protected Stream(TInfo info, IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            _log = Log.Logger.ForContext("Stream", $"[Stream:{info.Id}]");

            Status = new TStatus();
            Status.FromInfo(info);

            _cts = new CancellationTokenSource();
        }

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

        public StreamStatus Status { get; protected set; }

        public TStatus? CastedStatus => Status as TStatus;

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
            }

            _log.Information($"Consumers: {_consumers.Count}");

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
            _log.Information($"Consumers: {_consumers.Count}");
        }

        protected void BroadcastChunk(Chunk chunk)
        {
            lock (_lock)
            {
                if (_consumers.Count == 0) return;

                foreach (var consumer in _consumers)
                    consumer.Writer.TryWrite(chunk);
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
            SingleReader = false,
            SingleWriter = true
        });

        protected readonly Serilog.ILogger _log;

        protected IServiceProvider _serviceProvider;

        private Task? _task;
        private readonly CancellationTokenSource _cts;
        private readonly List<ConsumerSession> _consumers = [];
        private readonly Lock _lock = new();
    }
}
