using ComplianceTracker.Application.Frameworks;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ComplianceTracker.Infrastructure.Frameworks
{
    // Finds requirements in a PDF's text with PdfPig (US-007 R1). No AI (domain model rule 9).
    // A requirement starts at a line beginning with a numbered code followed by a title,
    // e.g. "A.5.15 Access control" or "5.15 Access control". The lines up to the next code are its description.
    // This fits ISO/IEC 27001 Annex A and similar documents; anything else is filled in by hand (R5).
    public partial class PdfRequirementExtractor : IRequirementExtractor
    {
        // Limits, so a huge or odd document cannot fill the database
        private const int MaxPages = 500;
        private const int MaxRequirements = 2000;
        private const int MaxTitleLength = 500;
        private const int MaxDescriptionLength = 4000;

        // "A.5.15 Access control", "5.15 Access control", "8.1.2 Title". The title must start with a letter.
        [GeneratedRegex(@"^(?<code>(?:[A-Z]{1,3}\.)?\d{1,3}(?:\.\d{1,3})+)\.?\s+(?<title>\p{L}.*)$")]
        private static partial Regex RequirementLine();

        // A table of contents line ends with dot leaders and a page number: "Access control ........ 12"
        [GeneratedRegex(@"\s*(?:\.\s*){3,}\d*\s*$")]
        private static partial Regex DotLeaders();

        public IReadOnlyList<ExtractedRequirement> Extract(byte[] pdf)
        {
            try
            {
                return FromLines(ReadLines(pdf));
            }
            catch (Exception)
            {
                // A PDF PdfPig cannot read is treated like one without text: the member adds requirements by hand
                return [];
            }
        }

        private static IEnumerable<string> ReadLines(byte[] pdf)
        {
            using var document = PdfDocument.Open(pdf);
            var lines = new List<string>();
            foreach (var page in document.GetPages().Take(MaxPages))
            {
                lines.AddRange(ContentOrderTextExtractor.GetText(page)
                    .Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0));
            }

            return lines;
        }

        // Exposed for unit tests through the public Extract; works on plain lines of text
        internal static IReadOnlyList<ExtractedRequirement> FromLines(IEnumerable<string> lines)
        {
            var found = new List<(string Code, string Title, List<string> Text)>();

            foreach (var line in lines)
            {
                var match = RequirementLine().Match(line);
                if (match.Success)
                {
                    var title = DotLeaders().Replace(match.Groups["title"].Value, "").Trim();
                    found.Add((match.Groups["code"].Value, title, new List<string>()));
                }
                else if (found.Count > 0)
                {
                    found[^1].Text.Add(line);
                }
            }

            // The same code twice usually means a table of contents and then the real text.
            // Keep the one with the most text, in the place it first appeared.
            return found
                .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(r => r.Text.Sum(t => t.Length)).First())
                .Take(MaxRequirements)
                .Select(r => new ExtractedRequirement(
                    r.Code,
                    Cut(r.Title, MaxTitleLength),
                    Cut(string.Join(" ", r.Text), MaxDescriptionLength)))
                .ToList();
        }

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }
}
