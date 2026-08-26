namespace Pege.Entities
{
    /// <summary>
    /// Порция аудиоданных.
    /// </summary>
    public class AudioChunk() : Chunk
    {
        /// <summary>
        /// ICY-метаданные.
        /// </summary>
        public byte[]? StreamMetadata { get; set; }
    }
}