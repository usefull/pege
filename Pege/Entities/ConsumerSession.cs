using System.Threading.Channels;

namespace Pege.Entities
{
    internal class ConsumerSession(Guid id, ChannelWriter<Chunk> writer, ChannelReader<Chunk> reader)
    {
        public readonly Guid Id = id;

        public readonly ChannelWriter<Chunk> Writer = writer;

        public readonly ChannelReader<Chunk> Reader = reader;
    }
}
