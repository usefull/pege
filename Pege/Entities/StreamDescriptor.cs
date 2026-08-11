using Pege.Data;
using Pege.Interfaces;
using Pege.Resource;
using System.ComponentModel.DataAnnotations;

namespace Pege.Entities
{
    /// <summary>
    /// Описание стрима.
    /// </summary>
    /// <remarks>Используется в контроллере для создания или изменения информации о стриме.</remarks>
    public class StreamDescriptor : IValidatableObject
    {
        /// <summary>
        /// Идентификатор стрима.
        /// </summary>
        [RegularExpression(@"^[a-zA-Z0-9\-\._~!\$&'\(\)\*\+,;=:@]+$",
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.InvalidStreamId))]
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamIdIsRequired)
        )]
        [MaxLength(50,
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamIdLengthExceeded))]
        public required string Id { get; set; }

        /// <summary>
        /// Название стрима.
        /// </summary>
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamTitleIsRequired)
        )]
        [MaxLength(255,
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamTitleLengthExceeded))]
        public required string Title { get; set; }

        /// <summary>
        /// Страна-источник стрима
        /// </summary>
        [MaxLength(100,
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamCountryLengthExceeded))]
        public string? Country { get; set; }

        /// <summary>
        /// Тип, реализующий функциональность стрима.
        /// </summary>
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.ImplTypeIsRequired)
        )]
        [MaxLength(150,
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamTypeLengthExceeded))]
        public required string ImplType { get; set; }

        /// <summary>
        /// Название Telegram-канала, в который стрим сможет отправлять сообщения.
        /// </summary>
        [MaxLength(50,
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamTgChannelLengthExceeded))]
        public string? TelegramChannelId { get; set; }

        /// <summary>
        /// Адрес ретранслируемого стрима или путь к файлу или каталогу с файлами.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Флаг указывает на то, что в метаданных аудио-стрима
        /// информацию о треке и артисте при ретрансляции нужно менять местами.
        /// </summary>
        public bool? MetadataSwap { get; set; }

        /// <summary>
        /// Метод валидации.
        /// </summary>
        /// <param name="validationContext">Контекст валидации.</param>
        /// <returns>Результаты валидации.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => [];

        public StreamStatus ToStatus()
        {
            Type statusType = null;
            var streamType = Type.GetType($"Pege.Streaming.{ImplType}");
            while (streamType != null && streamType != typeof(object))
            {
                if (streamType.IsGenericType && streamType.GetGenericTypeDefinition().Name.StartsWith("Stream`2"))
                {
                    statusType = streamType.GetGenericArguments()[0];
                    break;
                }
                streamType = streamType.BaseType;
            }
            if (statusType == null)
                throw new InvalidOperationException(string.Format(Error.UnknownStreamType, ImplType, Id));

            var status = Activator.CreateInstance(statusType) as StreamStatus
                    ?? throw new Exception(string.Format(Error.UnknownStreamType, ImplType, Id));

            status.FromDescription(this);
            return status;
        }

        public StreamInfo ToInfo() => ToStatus().ToInfo();
    }
}
