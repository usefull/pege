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

        public ConsumerPretender(int id, string streamUrl, HttpClient httpClient, TimeSpan? initialDelay = null)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
                throw new ArgumentException("URL потока не может быть пустым", nameof(streamUrl));

            _httpClient = httpClient;
            _id = id;
            _streamUrl = streamUrl;
            _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(500);
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

        /// <summary>
        /// Проверяет, является ли блок данных валидным ADTS-фреймом
        /// </summary>
        private bool TryParseAdtsFrame(List<byte> data, int offset, out int frameLength)
        {
            frameLength = 0;

            if (offset + 7 > data.Count)
                return false;

            // Проверка синхрослова
            if (data[offset] != 0xFF)
                return false;

            // Проверка второго байта (должен быть 0xF0-0xFF с определёнными битами)
            byte sync2 = data[offset + 1];
            if ((sync2 & 0xF6) != 0xF0)
                return false;

            // Проверка профиля (байт 2, биты 6-7)
            byte profile = (byte)((data[offset + 2] & 0xC0) >> 6);
            if (profile == 0 || profile > 3)
                return false;

            // Проверка частоты дискретизации (байт 2, биты 2-4)
            int sampleRateIndex = (data[offset + 2] & 0x3C) >> 2;
            if (sampleRateIndex == 0x0F) // 15 = зарезервировано
                return false;

            // Проверка конфигурации каналов (байт 2, биты 0-1 и байт 3, бит 7)
            int channelConfig = ((data[offset + 2] & 0x01) << 2) | ((data[offset + 3] & 0xC0) >> 6);
            if (channelConfig == 0 || channelConfig > 6)
                return false;

            // Читаем длину фрейма (байты 3,4,5)
            frameLength = ((data[offset + 3] & 0x03) << 11) |
                          (data[offset + 4] << 3) |
                          ((data[offset + 5] & 0xE0) >> 5);

            // Проверка, что длина разумная (для AAC обычно 100-5000 байт)
            if (frameLength < 50 || frameLength > 8192)
                return false;

            // Проверка, что фрейм полностью помещается в буфере
            if (offset + frameLength > data.Count)
                return false;

            return true;
        }

        private async Task ConnectAndConsumeStreamAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync(_streamUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await OnConnectionEstablishedAsync();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[65536];
                var leftover = new List<byte>(65536);
                var stopwatch = Stopwatch.StartNew();

                long lastChunkTime = 0;
                bool isFirstChunk = true;
                int frameCountInChunk = 0;
                int measureCount = 0;
                const int framesPerChunk = 15; // Количество ADTS-фреймов в одном чанке

                while (!cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0) break;

                    leftover.AddRange(buffer[0..bytesRead]);

                    int offset = 0;
                    while (offset + 7 <= leftover.Count)
                    {
                        if (TryParseAdtsFrame(leftover, offset, out int frameLength))
                        {
                            // Найден валидный ADTS-фрейм
                            frameCountInChunk++;

                            // Проверяем, завершился ли чанк (15 фреймов)
                            if (frameCountInChunk == framesPerChunk)
                            {
                                // Это конец чанка!
                                var now = stopwatch.ElapsedTicks;

                                if (!isFirstChunk)
                                {
                                    var delayMs = (now - lastChunkTime) * 1000.0 / Stopwatch.Frequency;

                                    // Логируем первые 50 замеров для клиента 0
                                    if (_id == 0 && ++measureCount <= 50)
                                    {
                                        Console.WriteLine($"[Client {_id}] Chunk #{measureCount}: delay = {delayMs:F2} ms, frames = {frameCountInChunk}");
                                    }

                                    // Вызываем событие СИНХРОННО
                                    DataDelay?.Invoke(this, new ConsumerDataDelayEventArgs
                                    {
                                        Id = _id,
                                        DelayMs = delayMs
                                    });
                                }
                                else
                                {
                                    isFirstChunk = false;
                                    if (_id == 0)
                                    {
                                        Console.WriteLine($"[Client {_id}] First complete chunk: {frameCountInChunk} frames");
                                    }
                                }

                                lastChunkTime = now;
                                frameCountInChunk = 0; // Сбрасываем для следующего чанка
                            }

                            offset += frameLength;
                            continue;
                        }

                        // Если это не валидный ADTS-фрейм, пробуем со следующего байта
                        offset++;
                    }

                    // Удаляем обработанные данные
                    if (offset > 0)
                    {
                        leftover.RemoveRange(0, offset);
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
                    ConnectionEstablished?.Invoke(this, new ConsumerEventArgs { Id = _id });
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
                ConnectionLost?.Invoke(this, new ConsumerErrorEventArgs { Id = _id, Exception = ex });
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void OnConnectionFailed(Exception ex)
        {
            ConnectionFailed?.Invoke(this, new ConsumerErrorEventArgs { Id = _id, Exception = ex });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isConnected = false;

            _cts.Cancel();
            _connectionLock.Dispose();
        }
    }
}