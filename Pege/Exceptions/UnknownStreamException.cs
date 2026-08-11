using Pege.Resource;

namespace Pege.Exceptions
{
    public class UnknownStreamException : Exception
    {
        public UnknownStreamException() : base(Error.UnknownStreamId) { }

        public UnknownStreamException(string? message) : base(message) { }

        public UnknownStreamException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
