namespace Pege.Interfaces
{
    public interface IChunk
    {
        ReadOnlyMemory<byte> Data { get; }
    }
}
