namespace Pege.Data
{
    /// <summary>
    /// Информация о ретрансляторе аудио-стрима.
    /// </summary>
    public class RelayAudioStreamInfo : RelayStreamInfo
    {
        /// <summary>
        /// Флаг указывает на то, что в метаданных аудио-стрима
        /// информацию о треке и артисте при ретрансляции нужно менять местами.
        /// </summary>
        public bool? MetadataSwap { get; set; }
    }
}
