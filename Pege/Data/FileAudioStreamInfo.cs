using Pege.Entities;
using Pege.Resource;
using System.ComponentModel.DataAnnotations;

namespace Pege.Data
{
    /// <summary>
    /// Информация о стриме на основе аудио-файлов.
    /// </summary>
    public class FileAudioStreamInfo : StreamInfo
    {
        /// <summary>
        /// Путь к файлу или каталогу с файлами.
        /// </summary>
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.PathIsRequired)
        )]
        public required string Path { get; set; }

        /// <summary>
        /// Метод создания экземпрляра типа <see cref="FileAudioStreamStatus"/>
        /// на основании полей этого типа.
        /// </summary>
        /// <returns>Экземпляр типа <see cref="FileAudioStreamStatus"/></returns>
        protected override StreamStatus CreateStatus() => new FileAudioStreamStatus()
        {
            Path = Path
        };
    }
}
