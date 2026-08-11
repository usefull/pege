using Pege.Data;
using Pege.Resource;
using System.Net.Sockets;
using System.Text;

namespace Pege.Streaming
{
    internal class RelayShoutcastV1AudioStream : BaseRelayAudioStream
    {
        public RelayShoutcastV1AudioStream(RelayAudioStreamInfo info, IServiceProvider serviceProvider) : base(info, serviceProvider)
        {
            CastedStatus?.ContentType = "audio/mpeg";
        }

        protected override async Task BroadcastCycleAsync(CancellationToken cancellationToken)
        {
            _log.Information(string.Format(Message.RetransmittingStarted, CastedStatus?.Uri) + " [SHOUTcast v1/ICY Mode]");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessIcyStreamRelayAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _log.Information(Message.RetransmittingStopped);
                    }
                    else
                    {
                        _log.Error(string.Format(Error.ConnectionLost, ex.Message));
                        await Task.Delay(10000, cancellationToken);
                    }
                }
            }
        }

        private async Task ProcessIcyStreamRelayAsync(CancellationToken cancellationToken)
        {
            var uri = new Uri(CastedStatus?.Uri);

            // Устанавливаем прямое TCP-соединение
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(uri.Host, uri.Port, cancellationToken);
            using var networkStream = tcpClient.GetStream();

            // Формируем текстовый HTTP/1.0 запрос вручную, маскируясь под плеер Winamp
            var requestBuilder = new StringBuilder();
            requestBuilder.AppendLine($"GET {uri.PathAndQuery} HTTP/1.0");
            requestBuilder.AppendLine($"Host: {uri.Host}:{uri.Port}");
            requestBuilder.AppendLine("User-Agent: WinampMPEG/5.0");
            requestBuilder.AppendLine("Accept: audio/mpeg, audio/aac, audio/*");
            requestBuilder.AppendLine("Icy-MetaData: 1");
            requestBuilder.AppendLine();

            byte[] requestBytes = Encoding.ASCII.GetBytes(requestBuilder.ToString());
            await networkStream.WriteAsync(requestBytes, cancellationToken);

            // Читаем текстовые заголовки ответа строго побайтово до \r\n\r\n
            // Побайтово, чтобы не проскочить начало аудиоданных.
            var headerBuilder = new StringBuilder();
            int b;
            while ((b = networkStream.ReadByte()) != -1)
            {
                headerBuilder.Append((char)b);

                // Проверяем, достигли ли мы конца заголовков (\r\n\r\n)
                if (headerBuilder.Length >= 4 &&
                    headerBuilder[^1] == '\n' && headerBuilder[^2] == '\r' &&
                    headerBuilder[^3] == '\n' && headerBuilder[^4] == '\r')
                {
                    break;
                }
            }

            // Разбираем полученные заголовки из строки
            string headersString = headerBuilder.ToString();
            string[] headerLines = headersString.Split(["\r\n"], StringSplitOptions.None);

            if (headerLines.Length == 0 || (!headerLines[0].Contains("200 OK") && !headerLines[0].Contains("200")))
            {
                throw new HttpRequestException(string.Format(Error.ShoutCastProtocolError, headerLines.Length > 0 ? headerLines[0] : Message.EmptyResponse));
            }

            int bitrate = 192;
            var metaInterval = 0;

            foreach (var line in headerLines)
            {
                if (line.StartsWith("icy-br:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line[7..].Trim(), out int br))
                        bitrate = br;
                }
                else if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                {
                    CastedStatus?.ContentType = line[13..].Trim();
                }
                else if (line.StartsWith("icy-metaint:", StringComparison.OrdinalIgnoreCase))
                {
                    _ = int.TryParse(line[12..].Trim(), out metaInterval);
                }
            }

            await RelayCycleAsync(networkStream, 8192, StreamReadTimeout, bitrate, metaInterval, cancellationToken);
        }

        private static readonly TimeSpan StreamReadTimeout = TimeSpan.FromSeconds(15);
    }
}
