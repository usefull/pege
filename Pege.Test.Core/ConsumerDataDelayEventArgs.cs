namespace Pege.Test.Core
{
    public class ConsumerDataDelayEventArgs : ConsumerEventArgs
    {
        public required double DelayMs { get; set; }
    }
}
