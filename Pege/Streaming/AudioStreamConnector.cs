
using Pege.Entities;
using Pege.Interfaces;
using System.Text;
using Telegram.Bot.Requests.Abstractions;

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
        public async Task ConsumeAsync(IStream stream, HttpRequest httpRequest, HttpResponse httpResponse, CancellationToken cancellationToken)
        {
            bool supportIcy = httpRequest.Headers.ContainsKey("Icy-MetaData")
                           && httpRequest.Headers["Icy-MetaData"] == "1";

            string userAgent = httpRequest.Headers.UserAgent.ToString() ?? "";
            bool isBrowser = userAgent.Contains("Mozilla") ||
                             userAgent.Contains("Chrome") ||
                             userAgent.Contains("Safari") ||
                             userAgent.Contains("Edge");

            httpResponse.ContentType = stream.Status.ContentType;
            httpResponse.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            httpResponse.Headers.Append("Pragma", "no-cache");
            httpResponse.Headers.Append("Connection", "keep-alive");
            httpResponse.Headers.Append("X-Content-Type-Options", "nosniff");
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
                        await httpResponse.Body.WriteAsync(chunk.Data, cancellationToken);
                        await httpResponse.Body.FlushAsync(cancellationToken);
                        continue;
                    }

                    ReadOnlyMemory<byte> audioData = chunk.Data;

                    while (audioData.Length > 0)
                    {
                        int bytesLeftInInterval = IcyMetaInterval - bytesSentInCurrentInterval;
                        int bytesToWrite = Math.Min(audioData.Length, bytesLeftInInterval);

                        await httpResponse.Body.WriteAsync(audioData[..bytesToWrite], cancellationToken);
                        bytesSentInCurrentInterval += bytesToWrite;
                        audioData = audioData[bytesToWrite..];

                        if (bytesSentInCurrentInterval == IcyMetaInterval)
                        {
                            // Проверяем, изменился ли трек
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
                        }
                    }

                    //await httpResponse.Body.FlushAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
            finally
            {
                stream.Unsubscribe(sessionId);
            }
        }
    }
}
