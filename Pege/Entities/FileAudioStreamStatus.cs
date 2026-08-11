using System.Text.Json.Serialization;

namespace Pege.Entities
{
    /// <summary>
    /// Состояние стрима на основе аудио-файлов.
    /// </summary>
    public class FileAudioStreamStatus : AudioStreamStatus
    {
        /// <summary>
        /// Путь к файлу или каталогу с файлами.
        /// </summary>
        [JsonIgnore]
        public string? Path { get; set; }

        /// <summary>
        /// Название текущего трека
        /// </summary>
        public string? NextTrack { get; set; }

        /// <summary>
        /// Название текущего артиста.
        /// </summary>
        public string? NextArtist { get; set; }

        /// <summary>
        /// Количество треков в плейлисте стрима.
        /// </summary>
        public int TotalTracks { get; set; }

        /// <summary>
        /// Общая продолжительность.
        /// </summary>
        public TimeSpan TotalDuration { get; set; }
    }
}
