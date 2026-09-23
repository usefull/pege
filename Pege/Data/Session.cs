using System.ComponentModel.DataAnnotations;

namespace Pege.Data
{
    /// <summary>
    /// Запись о сессии слушателя.
    /// Регистрируется при подключении к стриму
    /// и обновляется при отключении.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// Идентификатор сессии.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Идентификатор стрима, к которому подключён слушатель.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string? StreamId { get; set; }

        /// <summary>
        /// IP-адрес слушателя.
        /// </summary>
        [StringLength(45)]
        public string? Ip { get; set; }

        /// <summary>
        /// User-Agent клиента.
        /// </summary>
        [StringLength(512)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Гео-информация, полученная от геосервиса.
        /// </summary>
        [StringLength(100)]
        public string? Geo { get; set; }

        /// <summary>
        /// Дата/время подключения (UTC).
        /// </summary>
        [Required]
        public DateTime Connected { get; set; }

        /// <summary>
        /// Дата/время отключения (UTC).
        /// null — сессия ещё активна.
        /// </summary>
        public DateTime? Closed { get; set; }

        /// <summary>
        /// Дата/время обновления информации о сессии (UTC).
        /// </summary>
        public DateTime? Updated { get; set; }

        /// <summary>
        /// Количество байт, отданных слушателю за сессию.
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// Причина закрытия сессии.
        /// null — сессия ещё активна.
        /// </summary>
        public SessionCloseReason? CloseReason { get; set; }
    }
}