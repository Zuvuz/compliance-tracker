namespace ComplianceTracker.Domain.Frameworks
{
    // US-137 R3: a part of an act that is not a requirement for the manufacturer, and why, e.g. ("Recital (12)", "Recital")
    public sealed record LeftOutPart(string Code, string Reason);

    // US-137 R4: one group of a standard's profile, e.g. "controls": how many were expected and found, which are missing,
    // and codes that look like the group's but are not in the standard
    public sealed record CompletenessGroup(string Name, int Expected, int Found, IReadOnlyList<string> Missing, IReadOnlyList<string> Extra)
    {
        // "92 of 93 controls found"
        public string Text => $"{Found} of {Expected} {Name} found";
    }

    // US-137: how complete a draft version is, kept with the draft so it can be checked before activation (R4, R5). For an EU
    // act imported from EUR-Lex: its parts and what was left out (R1–R3). For a standard with a known profile: the expected
    // codes found and missing (R4). The profile holds codes and counts only, never a standard's text.
    public sealed record CompletenessCheck(
        string Source,
        int Articles,
        int Annexes,
        int Paragraphs,
        int Points,
        int Requirements,
        IReadOnlyList<LeftOutPart> LeftOut,
        string? Profile,
        IReadOnlyList<CompletenessGroup> Groups)
    {
        public const int MaxListed = 500;

        // R1: the check of an EU act read from EUR-Lex (it has articles or annexes), not of an uploaded standard
        public bool IsOfEuAct => Articles > 0 || Annexes > 0;

        // R5: something is missing from a known profile
        public bool HasGaps => Groups.Any(g => g.Missing.Count > 0);

        // "Recital: 128, Definition: 51"
        public IReadOnlyList<(string Reason, int Count)> LeftOutByReason
            => LeftOut.GroupBy(p => p.Reason).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList();

        // R5: for the log, e.g. "EUR-Lex 32024R2847: 71 articles, 8 annexes; 140 requirements; 200 parts left out" or
        // "ISO/IEC 27001:2022: 92 of 93 controls found; A.5.23 is missing"
        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (Articles > 0 || Annexes > 0)
                {
                    parts.Add($"{Source}: {Count(Articles, "article")}, {Count(Annexes, "annex", "annexes")}");
                    parts.Add(Count(Requirements, "requirement"));
                    parts.Add($"{Count(LeftOut.Count, "part")} left out");
                }

                if (Profile is not null)
                {
                    parts.Add($"{Profile}: " + string.Join(", ", Groups.Select(g => g.Text)));
                    var missing = Groups.SelectMany(g => g.Missing).ToList();
                    if (missing.Count > 0)
                    {
                        parts.Add(missing.Count <= 5 ? string.Join(", ", missing.Select(m => $"{m} is missing")) : $"{missing.Count} codes are missing");
                    }
                }

                return string.Join("; ", parts);
            }
        }

        private static string Count(int n, string one, string? many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";
    }
}
