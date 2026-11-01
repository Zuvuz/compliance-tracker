namespace ComplianceTracker.Application.Common
{
    // "Today" for rules that follow the calendar, e.g. the day a framework version starts to apply (US-035).
    // It is the date in the server's local time zone, from the same clock as everything else (US-016),
    // so the tests can set the day.
    public static class ClockExtensions
    {
        public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    }
}
