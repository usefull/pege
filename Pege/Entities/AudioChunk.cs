namespace Pege.Entities
{
    public class AudioChunk() : Chunk
    {
        public int BitrateKbps { get; set; }

        public int DurationMs { get; set; }

        public byte[]? StreamMetadata { get; set; }
    }
}
