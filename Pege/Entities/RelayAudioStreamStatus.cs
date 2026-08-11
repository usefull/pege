using Pege.Data;
using System.Text.Json.Serialization;

namespace Pege.Entities
{
    /// <summary>
    /// Статус стрима, ретранслирующего аудио поток.
    /// </summary>
    public class RelayAudioStreamStatus : AudioStreamStatus
    {
        /// <summary>
        /// Адрес ретранслируемого потока.
        /// </summary>
        [JsonIgnore]
        public string? Uri { get; set; }

        /// <summary>
        /// Флаг указывает на то, что в метаданных аудио-стрима
        /// информацию о треке и артисте при ретрансляции нужно менять местами.
        /// </summary>
        [JsonIgnore]
        public bool? MetadataSwap { get; set; }

        public override void FromDescription(StreamDescriptor streamDescriptor)
        {
            base.FromDescription(streamDescriptor);

            Uri = streamDescriptor.Source;
            MetadataSwap = streamDescriptor.MetadataSwap;
        }

        protected override StreamInfo CreateInfo() => new RelayAudioStreamInfo
        {
            Uri = Uri,
            MetadataSwap = MetadataSwap
        };
    }
}