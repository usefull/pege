namespace Pege.Entities
{
    /// <summary>
    /// Состояние аудио-стрима.
    /// </summary>
    public abstract class AudioStreamStatus : StreamStatus
    {
        /// <summary>
        /// Название текущего трека
        /// </summary>
        public string? Track { get; set; }

        /// <summary>
        /// Название текущего артиста.
        /// </summary>
        public string? Artist { get; set; }
    }
}
