namespace Pege.Test.Core
{
    public class PeriodSummary
    {
        public int PeriodIndex { get; set; }
        public long Count { get; set; }
        public double Avg { get; set; }
        public double Median { get; set; }
        public double P99 { get; set; }
        public double Max { get; set; }
        public double Jitter { get; set; }
        public double StdDev { get; set; }

        public TimeSpan LNA { get; set; }
        public TimeSpan Encoding { get; set; }
        public TimeSpan ADTS { get; set; }
    }
}
