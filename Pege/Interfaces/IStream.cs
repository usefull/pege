using Pege.Entities;
using System.Threading.Channels;

namespace Pege.Interfaces
{
    /// <summary>
    /// Интерфейс сервиса, реализующего стрим.
    /// </summary>
    public interface IStream : IDisposable
    {
        void Start();

        /// <summary>
        /// Состояние стрима.
        /// </summary>
        StreamStatus Status { get; }

        /// <summary>
        /// Метод подключения к стриму.
        /// </summary>
        /// <returns>Канал для получения данных стрима и идентификатор сессии.</returns>
        (ChannelReader<Chunk> Reader, Guid SessionId) Subscribe();

        /// <summary>
        /// Метод отключения от стрима.
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии.</param>
        void Unsubscribe(Guid sessionId);
    }
}