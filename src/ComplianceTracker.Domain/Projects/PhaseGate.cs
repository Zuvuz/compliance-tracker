using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Projects
{
    // US-071, US-072: what must be in place before a project moves on. A gate warns and asks for a reason; it never
    // blocks (B89). Each check is worked out by the tracker itself (US-071 R5).
    public sealed record PhaseGate(string Name, LifecyclePhase From, LifecyclePhase To, IReadOnlyList<GateCheck> Checks)
    {
        public const int MaxReasonLength = 1000;

        // US-047 R5: the checklist items missing in the phases the move leaves behind, e.g. "Threat model made (CRA Art. 13(3))".
        // They warn and are logged, but need no reason.
        public IReadOnlyList<string> ChecklistMissing { get; init; } = [];

        // US-072 R4: a check that depends on something not built yet does not count
        public IReadOnlyList<GateCheck> NotMet => Checks.Where(c => c.State == GateCheckState.NotMet).ToList();

        public bool IsMet => NotMet.Count == 0;

        // US-071 R3: moving on with a check not met needs a reason. Returns the cleaned reason (null when every check is met).
        public string? Pass(string? reason)
        {
            if (IsMet)
            {
                return null;
            }

            var cleaned = reason?.Trim();
            if (string.IsNullOrEmpty(cleaned))
            {
                throw new DomainException($"Give a reason, as {Count(NotMet.Count)} not met");
            }

            if (cleaned.Length > MaxReasonLength)
            {
                throw new DomainException($"The reason must be {MaxReasonLength} characters or fewer");
            }

            return cleaned;
        }

        // R3: what the log says, e.g. "Phase: Planning → Development, 1 check not met: Risk workshop next week"
        public string Describe(ProjectChange change, string? reason)
            => Checks.Count == 0 ? change.ToString()
                : IsMet ? $"{change}, all checks met" : $"{change}, {NotMet.Count} {(NotMet.Count == 1 ? "check" : "checks")} not met: {reason}";

        // US-047 R5: logged next to the phase change, e.g. "Verification → Released, missing: Support period set (CRA Art. 13(8))"
        public string DescribeMissing() => $"{From} → {To}, missing: {string.Join(", ", ChecklistMissing)}";

        private static string Count(int count) => count == 1 ? "1 check is" : $"{count} checks are";
    }

    // One check: what it says in its current state, and a key the page uses to link to where it is fixed (R2)
    public sealed record GateCheck(string Key, string Text, GateCheckState State);

    public enum GateCheckState
    {
        Met,
        NotMet,
        NotAvailable
    }
}
