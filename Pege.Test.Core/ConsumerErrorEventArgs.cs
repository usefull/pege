namespace Pege.Test.Core
{
    public class ConsumerErrorEventArgs : ConsumerEventArgs
    {
        public required Exception Exception { get; set; }
    }
}
