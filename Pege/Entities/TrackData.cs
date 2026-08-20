namespace Pege.Entities
{
    /// <summary>
    /// Полные данные о треке, необходимые для воспроизведения.
    /// </summary>
    public class TrackData
    {
        /// <summary>
        /// Путь к файлу.
        /// </summary>
        public string Filename { get; set; } = string.Empty;

        /// <summary>
        /// Название трека.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Название испольнителя.
        /// </summary>
        public string Artist { get; set; } = "Unknowh Artist";

        /// <summary>
        /// Название кодека.
        /// </summary>
        public string? Codec { get; set; }

        /// <summary>
        /// Продолжительность.
        /// </summary>
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Частота дискретизации аудио в Гц.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Размер фрейма в сэмплах.
        /// </summary>
        public int SamplesPerFrame { get; set; }

        /// <summary>
        /// Чанки.
        /// </summary>
        public Queue<ReadOnlyMemory<byte>>? Chunks { get; set; }
    }
}
