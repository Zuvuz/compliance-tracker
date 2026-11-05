namespace ComplianceTracker.Domain.Projects
{
    // One field of a project that was changed, with its old and new value (US-024 R3).
    // Written to the activity log as e.g. "Phase: Planning → Development".
    public record ProjectChange(string Field, string OldValue, string NewValue)
    {
        public override string ToString() => $"{Field}: {Show(OldValue)} → {Show(NewValue)}";

        private static string Show(string value) => value.Length == 0 ? "(empty)" : value;
    }
}
