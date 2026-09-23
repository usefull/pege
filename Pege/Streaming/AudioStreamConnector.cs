using Pege.Data;
using Pege.Entities;
using Pege.Extensions;
using Pege.Interfaces;
using Serilog;
using System.Diagnostics;
using System.Text;

namespace Pege.Streaming
{
    /// <summary>
    /// Коннектор аудио-стрима и контроллера.
    /// </summary>
    /// <param name="sessionRegistry">Сервис регистрации сессий слушателей.</param>
    internal partial class AudioStreamConnector(ISessionRegistry sessionRegistry) : IConnector
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
            _delayMeasurementMode = config.GetSection("DelayMeasurementMode").Get<bool>();
            if (_delayMeasurementMode) StartMeasurement(config);

            bool supportIcy = httpRequest.Headers.ContainsKey("Icy-MetaData")
               && httpRequest.Headers["Icy-MetaData"] == "1";

            string userAgent = httpRequest.Headers.UserAgent.ToString() ?? string.Empty;
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
            _ = sessionRegistry.RegisterNewAsync(sessionId, stream.Status.Id ?? string.Empty, httpRequest.HttpContext.GetClientIp(), userAgent);

            int bytesSentInCurrentInterval = 0;
            byte[]? currentMetadata = null;
            var closeReason = SessionCloseReason.ClientDisconnect;

            try
            {
                using var timeoutCts0 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linkedCts0 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts0.Token);

                await httpResponse.Body.FlushAsync(linkedCts0.Token);

                await foreach (var chunk in reader.ReadAllAsync(cancellationToken).Cast<AudioChunk>())
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    byte[]? pendingMetadata = chunk.StreamMetadata;
                    if (!supportIcy)
                    {
                        var ts = Stopwatch.GetTimestamp();

                        await httpResponse.Body.WriteAsync(chunk.Data, linkedCts.Token);
                        _ = sessionRegistry.UpdateAsync(sessionId, chunk.Data.Length);

                        if (_delayMeasurementMode)
                        {
                            double valueMs = Stopwatch.GetElapsedTime(ts).TotalMilliseconds;
                            _meter?.Apply(valueMs);
                        }

                        continue;
                    }

                    ReadOnlyMemory<byte> audioData = chunk.Data;

                    while (audioData.Length > 0)
                    {
                        int bytesLeftInInterval = IcyMetaInterval - bytesSentInCurrentInterval;
                        int bytesToWrite = Math.Min(audioData.Length, bytesLeftInInterval);

                        await httpResponse.Body.WriteAsync(audioData[..bytesToWrite], linkedCts.Token);
                        _ = sessionRegistry.UpdateAsync(sessionId, bytesToWrite);

                        if (bytesSentInCurrentInterval + bytesToWrite == IcyMetaInterval)
                        {
                            if (pendingMetadata != null && !pendingMetadata.SequenceEqual(currentMetadata ?? ReadOnlySpan<byte>.Empty))
                            {
                                currentMetadata = pendingMetadata;
                                await httpResponse.Body.WriteAsync(currentMetadata, linkedCts.Token);
                            }
                            else
                            {
                                await httpResponse.Body.WriteAsync(EmptyMetaBlock, linkedCts.Token);
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

                closeReason = SessionCloseReason.Kicked;
            }
            catch (Exception ex) when (ex is OperationCanceledException) 
            {
                if (!cancellationToken.IsCancellationRequested)
                    closeReason = SessionCloseReason.Timeout;
            }
            catch (Exception ex)
            {
                closeReason = SessionCloseReason.Error;
                throw;
            }
            finally
            {
                stream.Unsubscribe(sessionId);
                _ = sessionRegistry.SetClosedAsync(sessionId, closeReason);
            }
        }

        protected readonly Serilog.ILogger _log = Log.Logger.ForContext("Stream", $"[Connector]");
    }
}