using Pege.Resource;

namespace Pege.Exceptions
{
    public class StreamUnavailableException : Exception
    {
        public StreamUnavailableException() : base(Error.StreamUnavailable) { }

        public StreamUnavailableException(string? message) : base(message) { }

        public StreamUnavailableException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
