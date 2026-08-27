namespace Pege.Entities
{
    /// <summary>
    /// Порция аудиоданных.
    /// </summary>
    public class AudioChunk : Chunk
    {
        public byte[]? StreamMetadata { get; init; }
    }
}