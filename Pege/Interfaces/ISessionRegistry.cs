using Pege.Data;

namespace Pege.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса регистрации сессий слушателей.
    /// </summary>
    public interface ISessionRegistry
    {
        /// <summary>
        /// Метод регистрации новой сессии.
        /// </summary>
        /// <param name="id">Идентификатор сессии.</param>
        /// <param name="streamId">Идентификатор потока.</param>
        /// <param name="ip">IP-адрес.</param>
        /// <param name="userAgent">Строка User-Agent</param>
        /// <returns>Задача, представляющая асинхронную операцию регистрации новой сессии.</returns>
        Task RegisterNewAsync(Guid id, string streamId, string? ip, string userAgent);

        /// <summary>
        /// Метод обновления данных о состоянии сессии.
        /// </summary>
        /// <param name="id">Идентификатор сессии.</param>
        /// <param name="bytesSent">Количество отправленных байтов.
        /// Это не полное количество байтов с начала сессии, а размер очередной отпрвленной порции.</param>
        /// <returns>Задача, представляющая асинхронную операцию обновления состояния сессии.</returns>
        Task UpdateAsync(Guid id, int bytesSent);

        /// <summary>
        /// Метод помечает сессию как закрытую.
        /// </summary>
        /// <param name="id">Идентификатор сессии.</param>
        /// <param name="reason">Причина закрытия.</param>
        /// <returns>Задача, представляющая асинхронную операцию обновления состояния сессии.</returns>
        Task SetClosedAsync(Guid id, SessionCloseReason reason = SessionCloseReason.ClientDisconnect);

        /// <summary>
        /// Метод помечает все активные сессии как закрытые по причине остановки сервера.
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию обновления состояния сессий.</returns>
        Task SetAllClosedAsync();
    }
}
