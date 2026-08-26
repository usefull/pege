using Pege.Entities;
using Pege.Resource;

namespace Pege.Streaming
{
    internal class RelayAudioStream(RelayAudioStreamStatus status, IServiceProvider serviceProvider) : BaseRelayAudioStream(status, serviceProvider)
    {
        private static readonly HttpClient _httpClient;
        //private const int DefaultBitrate = 192;
        private const int BufferSize = 8192;
        private static readonly TimeSpan StreamReadTimeout = TimeSpan.FromSeconds(15);

        static RelayAudioStream()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                AllowAutoRedirect = true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        protected override async Task BroadcastCycleAsync(CancellationToken cancellationToken)
        {
            _log.Information(string.Format(Message.RetransmittingStarted, CastedStatus.Uri));

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessStreamRelayAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                        _log.Information(Message.RetransmittingStopped);
                    else
                    {
                        _log.Error(string.Format(Error.ConnectionLost, ex.Message));
                        await Task.Delay(10000, cancellationToken);
                    }
                }
            }
        }

        private async Task ProcessStreamRelayAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, CastedStatus.Uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("Icy-MetaData", "1");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            CastedStatus.ContentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";

            int metaInterval = 0;
            if (response.Headers.TryGetValues("icy-metaint", out var strMetaInt))
                _ = int.TryParse(strMetaInt?.FirstOrDefault(), out metaInterval);

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await RelayCycleAsync(responseStream, BufferSize, StreamReadTimeout, metaInterval, cancellationToken);
        }
    }
}
