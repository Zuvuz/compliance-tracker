using System.Text.RegularExpressions;

namespace ComplianceTracker.Infrastructure.WorkItems
{
    // Reads the sections of an issue made from .github/ISSUE_TEMPLATE/user-story.yml (US-018 R3, R4).
    // GitHub writes an issue form as "### <label>" headings followed by the answer.
    public static partial class BddIssueTemplate
    {
        // What GitHub writes for an optional field that was left empty
        private const string NoResponse = "_No response_";

        [GeneratedRegex(@"^###\s+(?<heading>.+?)\s*$", RegexOptions.Multiline)]
        private static partial Regex Heading();

        [GeneratedRegex(@"^\s*```[a-zA-Z]*\s*\n(?<code>.*?)\n\s*```\s*$", RegexOptions.Singleline)]
        private static partial Regex Fenced();

        // The rules, examples and Gherkin, or null when the issue has no rules (no acceptance criteria)
        public static (string Rules, string Examples, string Gherkin)? Parse(string? body)
        {
            var sections = Sections(body ?? "");
            var rules = Section(sections, "Rules");
            if (rules.Length == 0)
            {
                return null;
            }

            return (rules, Section(sections, "Examples"), Unfenced(Section(sections, "Gherkin")));
        }

        private static Dictionary<string, string> Sections(string body)
        {
            var text = body.Replace("\r\n", "\n");
            var matches = Heading().Matches(text);
            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index + matches[i].Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                sections.TryAdd(matches[i].Groups["heading"].Value, text[start..end].Trim());
            }

            return sections;
        }

        private static string Section(Dictionary<string, string> sections, string heading)
            => sections.TryGetValue(heading, out var value) && value != NoResponse ? value : "";

        // The template renders Gherkin as a ```gherkin block; keep only the code
        private static string Unfenced(string value)
        {
            var match = Fenced().Match(value);
            return match.Success ? match.Groups["code"].Value.Trim() : value;
        }
    }
}
