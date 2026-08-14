
using Pege.Entities;
using Pege.Interfaces;
using Serilog;
using System.Diagnostics;
using System.Net.NetworkInformation;
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

            //httpRequest.HttpContext.Response.Headers.Remove("Transfer-Encoding");

            httpResponse.ContentType = stream.Status.ContentType;
            //httpResponse.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            httpResponse.Headers.Append("Cache-Control", "no-cache, no-store");
            //httpResponse.Headers.Append("Pragma", "no-cache");
            httpResponse.Headers.Append("Connection", "Close");
            //httpResponse.Headers.Append("X-Content-Type-Options", "nosniff");
            httpResponse.Headers.Append("Access-Control-Expose-Headers", "icy-metaint, icy-pub, icy-name");

            httpResponse.Headers["icy-genre"] = "Mixed";
            httpResponse.Headers["icy-description"] = "A sync-driven custom radio streaming server";
            httpResponse.Headers["Server"] = "Icecast 2.4.0-kh10";
            httpResponse.Headers["Access-Control-Allow-Origin"] = "*";
            httpResponse.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS, HEAD";
            httpResponse.Headers["Access-Control-Allow-Headers"] = "Origin, Accept, X-Requested-With, Content-Type";

            httpResponse.Headers.Append("icy-name", isBrowser
                ? Uri.EscapeDataString($"{stream.Status.Title} ||| {stream.Status.Country}")
                : Encoding.GetEncoding("ISO-8859-1").GetString(Encoding.UTF8.GetBytes($"{stream.Status.Title} ||| {stream.Status.Country}")));

            httpResponse.Headers.Append("icy-pub", "1");
            if (supportIcy)
                httpResponse.Headers.Append("icy-metaint", IcyMetaInterval.ToString());

            var (reader, sessionId) = stream.Subscribe();

            try
            {
                await httpResponse.Body.FlushAsync(cancellationToken);

                int bytesSentInCurrentInterval = 0;
                byte[]? currentMetadata = null;

                await foreach (var chunk in reader.ReadAllAsync(cancellationToken).Cast<AudioChunk>())
                {
                    byte[]? pendingMetadata = chunk.StreamMetadata;

                    // --- ВЕТКА А: Плеер НЕ поддерживает метаданные ---
                    if (!supportIcy)
                    {
                        await httpResponse.Body.WriteAsync(chunk.Data, cancellationToken);
                        await httpResponse.Body.FlushAsync(cancellationToken);
                        continue;
                    }

                    // --- ВЕТКА Б: Плеер ПОДДЕРЖИВАЕТ метаданные ---
                    ReadOnlyMemory<byte> audioData = chunk.Data;

                    while (audioData.Length > 0)
                    {
                        // Сколько байт аудио осталось отправить до следующей точки врезки метаданных
                        int bytesLeftInInterval = IcyMetaInterval - bytesSentInCurrentInterval;
                        int bytesToWrite = Math.Min(audioData.Length, bytesLeftInInterval);

                        // Отправляем порцию аудио
                        await httpResponse.Body.WriteAsync(audioData[..bytesToWrite], cancellationToken);

                        if (bytesSentInCurrentInterval + bytesToWrite == IcyMetaInterval)
                        {
                            // Мы дошли до точки врезки! 
                            // Проверяем, изменились ли метаданные (название трека) по сравнению с прошлым разом
                            if (pendingMetadata != null && !pendingMetadata.AsSpan().SequenceEqual(currentMetadata ?? ReadOnlySpan<byte>.Empty))
                            {
                                currentMetadata = pendingMetadata;
                                // Врезаем новые метаданные в поток
                                await httpResponse.Body.WriteAsync(currentMetadata, cancellationToken);
                            }
                            else
                            {
                                // Метаданные не изменились — шлем пустой блок (1 байт 0x00)
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

                    // Плавный выталкиватель пакетов в конце каждого чанка
                    await httpResponse.Body.FlushAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
            finally
            {
                stream.Unsubscribe(sessionId);
            }
        }

        protected readonly Serilog.ILogger _log = Log.Logger.ForContext("Stream", $"[Connector]");
    }
}
