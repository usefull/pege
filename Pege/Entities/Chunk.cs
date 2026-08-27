namespace Pege.Entities
{
    public abstract class Chunk
    {
        public ReadOnlyMemory<byte> Data { get; init; }
    }
}