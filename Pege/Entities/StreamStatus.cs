using Pege.Data;
using System.Text.Json.Serialization;

namespace Pege.Entities
{
    /// <summary>
    /// Базовый класс состояния стрима.
    /// </summary>
    public class StreamStatus
    {
        /// <summary>
        /// Идентификатор стрима.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Название стрима.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Страна-источник стрима
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// Тип MIME содержимого, транслируемого стримом.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Тип реализующий функциональность стрима.
        /// </summary>
        [JsonIgnore]
        public string? ImplType { get; set; }

        /// <summary>
        /// Дата-время запуска стрима.
        /// </summary>
        public DateTime? Started { get; set; }

        /// <summary>
        /// Дата-время остановки стрима.
        /// </summary>
        public DateTime? Stopped { get; set; }

        /// <summary>
        /// Дата-время регистрации стрима.
        /// </summary>
        public DateTime? Registered { get; set; }

        /// <summary>
        /// Название Telegram-канала, в который стрим сможет отправлять сообщения.
        /// </summary>
        [JsonIgnore]
        public string? TelegramChannelId { get; set; }

        /// <summary>
        /// Метод копирования данных из сущности <see cref="StreamInfo"/>.
        /// </summary>
        /// <param name="info">Сущность-источник.</param>
        public virtual void FromInfo(StreamInfo info)
        {
            Id = info.Id;
            Title = info.Title;
            Country = info.Country;
            Stopped = info.Stopped;
            Registered = info.Registered;
            ImplType = info.ImplType;
            TelegramChannelId = info.TelegramChannelId;
        }
    }
}