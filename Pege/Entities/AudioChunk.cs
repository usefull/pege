using Pege.Interfaces;

namespace Pege.Entities
{
    /// <summary>
    /// Порция аудиоданных.
    /// </summary>
    public readonly struct AudioChunk : IChunk
    {
        public ReadOnlyMemory<byte> Data { get; init; }

        public byte[]? StreamMetadata { get; init; }
    }
}