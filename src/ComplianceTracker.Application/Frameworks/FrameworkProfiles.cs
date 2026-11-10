using System.Text.RegularExpressions;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // US-137 R4: the known list of codes of a standard. Only codes and counts, which are facts about the standard's
    // structure; never its copyrighted text.
    public sealed record ProfileGroup(string Name, IReadOnlyList<string> Codes, Regex? LooksLike = null);

    public sealed record FrameworkProfile(string Name, Regex Matches, IReadOnlyList<ProfileGroup> Groups)
    {
        // R4: the draft's codes against the profile: found, missing, and codes that look like the group's but are not in it
        public IReadOnlyList<CompletenessGroup> Check(IEnumerable<string> draftCodes)
        {
            var codes = draftCodes.Select(c => c.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Groups.Select(group =>
            {
                var expected = group.Codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missing = group.Codes.Where(c => !codes.Contains(c)).ToList();
                var extra = group.LooksLike is null
                    ? []
                    : codes.Where(c => group.LooksLike.IsMatch(c) && !expected.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
                return new CompletenessGroup(group.Name, group.Codes.Count, group.Codes.Count - missing.Count,
                    missing.Take(CompletenessCheck.MaxListed).ToList(), extra.Take(CompletenessCheck.MaxListed).ToList());
            }).ToList();
        }
    }

    // B80: the first profiles. ISO/IEC 27001:2022 (clauses 4–10 and the 93 controls of Annex A) and ISO/IEC 27002:2022 (the
    // same 93 controls, numbered without "A.").
    public static partial class FrameworkProfiles
    {
        [GeneratedRegex(@"27001(?!\d)", RegexOptions.IgnoreCase)]
        private static partial Regex Iso27001();

        [GeneratedRegex(@"27002(?!\d)", RegexOptions.IgnoreCase)]
        private static partial Regex Iso27002();

        [GeneratedRegex(@"^A\.[5-8]\.\d{1,2}$", RegexOptions.IgnoreCase)]
        private static partial Regex AnnexAControl();

        [GeneratedRegex(@"^[5-8]\.\d{1,2}$")]
        private static partial Regex Control();

        // The controls of ISO/IEC 27001:2022 Annex A and ISO/IEC 27002:2022: 37 organizational, 8 people, 14 physical and
        // 34 technological
        private static IEnumerable<string> Controls(string prefix)
            => new[] { (5, 37), (6, 8), (7, 14), (8, 34) }.SelectMany(t => Enumerable.Range(1, t.Item2).Select(n => $"{prefix}{t.Item1}.{n}"));

        // The clauses of ISO/IEC 27001:2022 that hold requirements
        private static readonly string[] Iso27001Clauses =
        [
            "4.1", "4.2", "4.3", "4.4", "5.1", "5.2", "5.3", "6.1.1", "6.1.2", "6.1.3", "6.2", "6.3",
            "7.1", "7.2", "7.3", "7.4", "7.5.1", "7.5.2", "7.5.3", "8.1", "8.2", "8.3",
            "9.1", "9.2.1", "9.2.2", "9.3.1", "9.3.2", "9.3.3", "10.1", "10.2"
        ];

        public static readonly IReadOnlyList<FrameworkProfile> All =
        [
            new("ISO/IEC 27001:2022", Iso27001(),
            [
                new ProfileGroup("clauses", Iso27001Clauses),
                new ProfileGroup("controls", Controls("A.").ToList(), AnnexAControl())
            ]),
            new("ISO/IEC 27002:2022", Iso27002(),
            [
                new ProfileGroup("controls", Controls("").ToList(), Control())
            ])
        ];

        // The profile a framework's name points at, e.g. "ISO/IEC 27001" or "ISO 27001:2022"; null for any other
        public static FrameworkProfile? For(string frameworkName) => All.FirstOrDefault(p => p.Matches.IsMatch(frameworkName ?? ""));
    }
}
