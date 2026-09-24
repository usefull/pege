using Pege.Interfaces;
using Pege.Resource;
using Serilog;

namespace Pege.Services
{
    /// <summary>
    /// Сервис получения геоинформации по IP-адресу.
    /// </summary>
    public class GeoIpService(HttpClient httpClient) : IGeoIpService
    {
        /// <summary>
        /// Метод чтения местоположения по IP-адресу.
        /// </summary>
        /// <param name="ip">IP-адрес.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, представляющая асинхронную операцию чтения.</returns>
        public async Task<string?> GetGeoFromIpAsync(string? ip, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ip)) return null;

            string? result = null;

            try
            {
                var response = await httpClient.GetAsync($"json/{ip}", cancellationToken);
                response.EnsureSuccessStatusCode();
                var geo = await response.Content.ReadFromJsonAsync<Geo>(cancellationToken);
                result = geo?.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(string.Format(Error.GeoReadingError, ex.Message));
            }

            return result;
        }

        internal class Geo
        {
            public string? Country { get; set; }

            public string? RegionName { get; set; }

            public string? City { get; set; }

            public string? Org { get; set; }

            public override string ToString() =>
                string.Join(", ", new[] { Country, RegionName, City, Org }.Where(i => !string.IsNullOrWhiteSpace(i)));
        }
    }
}