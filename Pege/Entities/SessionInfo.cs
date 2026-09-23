using Pege.Data;

namespace Pege.Entities
{
    public class SessionInfo
    {
        private readonly object _lock = new();

        public Guid Id { get; init; }

        public bool IsNew { get; init; }

        public string StreamId { get; init; }

        public string? Ip { get; init; }

        public string? UserAgent { get; init; }

        public string? Geo { get; init; }

        public DateTime? Connected { get; init; }

        public long BytesSent { get; private set; }

        public DateTime? Updated { get; private set; }

        public DateTime? Closed { get; private set; }

        public SessionCloseReason? CloseReason { get; private set; }

        public void AddBytes(int bytes)
        {
            lock (_lock)
            {
                BytesSent += bytes;
                Updated = DateTime.UtcNow;
            }
        }

        public void Close(SessionCloseReason reason)
        {
            lock (_lock)
            {
                Closed = DateTime.UtcNow;
                CloseReason = reason;
            }
        }

        public Session ToSession() => new ()
        {
            Id = Id,
            StreamId = StreamId,
            Ip = Ip,
            UserAgent = UserAgent,
            Geo = Geo,
            Connected = Connected ?? DateTime.MinValue,
            BytesSent = BytesSent,
            Updated = Updated,
            Closed = Closed,
            CloseReason = CloseReason
        };
    }
}
