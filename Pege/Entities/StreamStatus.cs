using Pege.Data;
using System.Text.Json.Serialization;

namespace Pege.Entities
{
    /// <summary>
    /// Базовый класс состояния стрима.
    /// </summary>
    public abstract class StreamStatus
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
        /// Кол-во потребителей в настоящий момент.
        /// </summary>
        public int Consumers { get; set; }

        /// <summary>
        /// Название Telegram-канала, в который стрим сможет отправлять сообщения.
        /// </summary>
        [JsonIgnore]
        public string? TelegramChannelId { get; set; }

        public virtual void FromDescription(StreamDescriptor streamDescriptor)
        {
            Id = streamDescriptor.Id;
            Title = streamDescriptor.Title;
            Country = streamDescriptor.Country;
            ImplType = streamDescriptor.ImplType;
            TelegramChannelId = streamDescriptor.TelegramChannelId;
        }

        public StreamInfo ToInfo()
        {
            var info = CreateInfo();

            info.Id = Id?.Trim().ToLower();
            info.Title = Title;
            info.Country = Country;
            info.ImplType = ImplType;
            info.TelegramChannelId = TelegramChannelId;

            return info;
        }

        protected abstract StreamInfo CreateInfo();
    }
}