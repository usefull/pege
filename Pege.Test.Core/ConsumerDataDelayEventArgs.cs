namespace Pege.Test.Core
{
    public class ConsumerDataDelayEventArgs : ConsumerEventArgs
    {
        public required long Delay { get; set; }
    }
}
