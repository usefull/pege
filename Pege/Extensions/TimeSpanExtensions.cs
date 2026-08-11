namespace Pege.Extensions
{
    internal static class TimeSpanExtensions
    {
        public static string FormatTimeSpan(this TimeSpan ts)
        {
            var components = new (int Value, string Singular, string Plural)[]
            {
                (ts.Days, "day", "days"),
                (ts.Hours, "hour", "hours"),
                (ts.Minutes, "minute", "minutes"),
                (ts.Seconds, "second", "seconds")
            };

            var parts = components
                .Where(x => x.Value > 0)
                .Select(x => $"{x.Value} {(x.Value == 1 ? x.Singular : x.Plural)}")
                .ToList();

            if (parts.Count == 0)
                return "0 seconds";

            return string.Join(" ", parts);
        }
    }
}
