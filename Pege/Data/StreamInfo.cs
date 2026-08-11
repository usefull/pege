using Pege.Resource;
using System.ComponentModel.DataAnnotations;

namespace Pege.Data
{
    /// <summary>
    /// Базовый класс информации о стриме.
    /// </summary>
    public abstract class StreamInfo : IValidatableObject
    {
        /// <summary>
        /// Идентификатор стрима.
        /// </summary>
        [Key]
        [StringLength(50)]
        [RegularExpression(@"^[a-zA-Z0-9\-\._~!\$&'\(\)\*\+,;=:@]+$",
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.InvalidStreamId))]
        public string? Id { get; set; }

        /// <summary>
        /// Название стрима.
        /// </summary>
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.StreamTitleIsRequired)
        )]
        [StringLength(255)]
        public string? Title { get; set; }

        /// <summary>
        /// Страна-источник стрима
        /// </summary>
        [StringLength(100)]
        public string? Country { get; set; }

        /// <summary>
        /// Тип, реализующий функциональность стрима.
        /// </summary>
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.ImplTypeIsRequired)
        )]
        [StringLength(150)]
        public string? ImplType { get; set; }

        /// <summary>
        /// Дата/время регистрации стрима (создание записи).
        /// </summary>
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.RegisteredIsRequired)
        )]
        public DateTime? Registered { get; set; }

        /// <summary>
        /// Дата/время остановки стрима.
        /// </summary>
        public DateTime? Stopped { get; set; }

        /// <summary>
        /// Название Telegram-канала, в который стрим сможет отправлять сообщения.
        /// </summary>
        [StringLength(50)]
        public string? TelegramChannelId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => [];
    }
}