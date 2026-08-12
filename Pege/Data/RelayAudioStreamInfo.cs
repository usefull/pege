using Pege.Entities;

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

        /// <summary>
        /// Метод создания экземпрляра типа <see cref="RelayAudioStreamStatus"/>
        /// на основании полей этого типа.
        /// </summary>
        /// <returns>Экземпляр типа <see cref="RelayAudioStreamStatus"/></returns>
        protected override StreamStatus CreateStatus() => new RelayAudioStreamStatus()
        {
            Uri = Uri,
            MetadataSwap = MetadataSwap
        };

        /// <summary>
        /// Метод копирования данных из другой сущности.
        /// </summary>
        /// <param name="info">Сущность-источник.</param>
        public override void CopyFrom(StreamInfo info)
        {
            base.CopyFrom(info);

            if (info is RelayAudioStreamInfo relayInfo)
            {
                Uri = relayInfo.Uri;
                MetadataSwap = relayInfo.MetadataSwap;
            }
        }
    }
}
