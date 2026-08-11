using Pege.Resource;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public string? Path { get; set; }
    }
}
