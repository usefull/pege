namespace Pege.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса получения геоинформации по IP-адресу.
    /// </summary>
    public interface IGeoIpService
    {
        /// <summary>
        /// Метод чтения местоположения по IP-адресу.
        /// </summary>
        /// <param name="ip">IP-адрес.</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Задача, представляющая асинхронную операцию чтения.</returns>
        Task<string?> GetGeoFromIpAsync(string? ip, CancellationToken cancellationToken = default);
    }
}