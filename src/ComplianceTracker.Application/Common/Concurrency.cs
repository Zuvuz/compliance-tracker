using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Application.Common
{
    // US-092: noticing that someone else changed what a form was opened with.
    // A form carries the version it showed (PostgreSQL's xmin, set by the database). The use case compares it with
    // the version it loads; the unit of work catches a change made between the load and the save.
    public static class Concurrency
    {
        public const string Message = "Someone else changed this while you were editing. Reload to see their change";

        // expected: the version the form was opened with, or null when the caller does not know it
        public static void Ensure(uint current, uint? expected)
        {
            if (expected is { } version && version != current)
            {
                throw new DomainException(Message);
            }
        }
    }
}
