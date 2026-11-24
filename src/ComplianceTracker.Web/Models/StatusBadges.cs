namespace ComplianceTracker.Web.Models
{
    // US-168 R2: one status vocabulary with fixed colours, so a status looks the same on every page and in every report.
    // The colour is Bootstrap's (text-bg-*), plus ct-status-<slug> so the stylesheet and the tests can find a status.
    public static class StatusBadges
    {
        public enum Tone
        {
            Good,     // green: covered, done, low
            Warning,  // yellow: needs review, partly, medium
            Attention,// orange: needs action, high
            Bad,      // red: not covered, missing, critical
            Neutral,  // grey: not applicable, turned off, closed
            Ai,       // teal: AI-generated, AI-drafted
            Info      // light: phases, kinds and other plain labels
        }

        private static readonly Dictionary<string, Tone> Known = new(StringComparer.OrdinalIgnoreCase)
        {
            // Requirements (US-021, US-043, US-110, US-155)
            ["Covered"] = Tone.Good,
            ["Covered, needs review"] = Tone.Warning,
            ["Partly"] = Tone.Warning,
            ["Partly covered"] = Tone.Warning,
            ["Not covered"] = Tone.Bad,
            ["Not applicable"] = Tone.Neutral,
            ["Needs action"] = Tone.Attention,
            ["Needs review"] = Tone.Warning,

            // US-204 R9: proof is a state of its own next to coverage
            ["Proven"] = Tone.Good,
            ["Documented – proof missing"] = Tone.Warning,

            // AI (AI Act Art. 50: always marked)
            ["AI-generated"] = Tone.Ai,
            ["AI-drafted"] = Tone.Ai,

            // Checklists (US-047)
            ["Done"] = Tone.Good,
            ["Missing"] = Tone.Bad,
            ["Turned off"] = Tone.Neutral,

            // Risk levels (US-042)
            ["Low"] = Tone.Good,
            ["Medium"] = Tone.Warning,
            ["High"] = Tone.Attention,
            ["Critical"] = Tone.Bad,

            // States
            ["Open"] = Tone.Info,
            ["Closed"] = Tone.Neutral,
            ["Accepted"] = Tone.Neutral,
            ["Treated"] = Tone.Good,
            ["Expired"] = Tone.Bad,

            // US-099: deadlines on "My tasks"
            ["Overdue"] = Tone.Bad,
            ["Due soon"] = Tone.Attention,
            ["Later"] = Tone.Neutral,

            // US-028: an archived project
            ["Archived"] = Tone.Neutral,

            // US-208: the ISMS scope's version and a project's place in it
            ["Approved"] = Tone.Good,
            ["Not approved"] = Tone.Warning,
            ["Changed since approval"] = Tone.Warning,
            ["Inside"] = Tone.Good,
            ["Outside"] = Tone.Neutral,
            ["Not decided"] = Tone.Warning
        };

        public static Tone ToneOf(string? status) => status is not null && Known.TryGetValue(status.Trim(), out var tone) ? tone : Tone.Info;

        // e.g. "Not covered" -> "badge text-bg-danger ct-status ct-status-not-covered"
        public static string ClassOf(string? status)
            => $"badge {BootstrapClass(ToneOf(status))} ct-status ct-status-{Slug(status)}";

        // #52: the colour only, for a table cell or a block: "badge" would make a td an inline pill
        // e.g. "High" -> "ct-bg-attention ct-status-high"
        public static string CellClassOf(string? status)
            => $"{BootstrapClass(ToneOf(status))} ct-status-{Slug(status)}";

        public static string BootstrapClass(Tone tone) => tone switch
        {
            Tone.Good => "text-bg-success",
            Tone.Warning => "text-bg-warning",
            Tone.Attention => "ct-bg-attention",
            Tone.Bad => "text-bg-danger",
            Tone.Neutral => "text-bg-secondary",
            Tone.Ai => "text-bg-info",
            _ => "text-bg-light border"
        };

        public static string Slug(string? status)
        {
            var text = new string((status ?? "").Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
            while (text.Contains("--", StringComparison.Ordinal))
            {
                text = text.Replace("--", "-", StringComparison.Ordinal);
            }

            return text.Trim('-') is { Length: > 0 } slug ? slug : "none";
        }
    }
}
