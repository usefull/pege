
using System.Diagnostics;

namespace Pege.Test.Core
{
    public class ConsumerPretender : IDisposable
    {
        private readonly int _id;
        private readonly string _streamUrl;
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts;
        private readonly SemaphoreSlim _connectionLock;
        private readonly TimeSpan _initialDelay;
        private readonly Task _playerTask;
        private bool _disposed;
        private bool _isConnected;

        public event EventHandler<ConsumerErrorEventArgs>? ConnectionLost;
        public event EventHandler<ConsumerEventArgs>? ConnectionEstablished;
        public event EventHandler<ConsumerDataDelayEventArgs>? DataDelay;
        public event EventHandler<ConsumerErrorEventArgs>? ConnectionFailed;

        public ConsumerPretender(int id, string streamUrl, TimeSpan? initialDelay = null)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
                throw new ArgumentException("URL потока не может быть пустым", nameof(streamUrl));

            _id = id;
            _streamUrl = streamUrl;
            _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(500);
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = int.MaxValue,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            _cts = new CancellationTokenSource();
            _connectionLock = new SemaphoreSlim(1, 1);

            _playerTask = Task.Run(() => RunPlayerAsync(_cts.Token));
        }

        private async Task RunPlayerAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(_initialDelay, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndConsumeStreamAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ConnectAndConsumeStreamAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync(_streamUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await OnConnectionEstablishedAsync();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[8192];
                int bytesRead;

                var stopwatch = new Stopwatch();

                while (true)
                {
                    stopwatch.Restart();

                    bytesRead = await stream.ReadAsync(buffer, cancellationToken);

                    stopwatch.Stop();

                    if (bytesRead <= 0)
                        break;

                    if (stopwatch.ElapsedMilliseconds > 2000)
                    {
                        var delay = stopwatch.ElapsedMilliseconds;
                        _ = Task.Run(() => DataDelay?.Invoke(this, new ConsumerDataDelayEventArgs { Id = _id, Delay = delay }));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_isConnected)
                    OnConnectionLost(ex);
                else
                    OnConnectionFailed(ex);
                throw;
            }
        }

        private async Task OnConnectionEstablishedAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (!_isConnected)
                {
                    _isConnected = true;
                    
                    _ = Task.Run(() => ConnectionEstablished?.Invoke(this, new ConsumerEventArgs { Id = _id }));
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void OnConnectionLost(Exception ex)
        {
            _connectionLock.Wait();
            try
            {
                _isConnected = false;

                Task.Run(() => ConnectionLost?.Invoke(this, new ConsumerErrorEventArgs { Id = _id, Exception = ex }));
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void OnConnectionFailed(Exception ex)
        {
            Task.Run(() => ConnectionFailed?.Invoke(this, new ConsumerErrorEventArgs { Id = _id, Exception = ex }));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isConnected = false;

            _cts.Cancel();

            _httpClient.Dispose();
            _connectionLock.Dispose();
        }
    }
}
