using Pege.Interfaces;
using System.Threading.Channels;

namespace Pege.Entities
{
    internal class ConsumerSession(Guid id, ChannelWriter<IChunk> writer, ChannelReader<IChunk> reader)
    {
        public readonly Guid Id = id;

        public readonly ChannelWriter<IChunk> Writer = writer;

        public readonly ChannelReader<IChunk> Reader = reader;
    }
}
