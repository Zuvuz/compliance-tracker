namespace ComplianceTracker.Domain.Frameworks
{
    // US-070: text on a page that did not become a requirement, cut to 300 characters (R2)
    public sealed record LeftOverText(int Page, string Text);

    // US-070: what the extraction from a PDF did not use, kept with the draft version so the admin can check it before
    // activating (R1–R11). Made once at upload; the page leaves out what later hand-added requirements cover (R6).
    public sealed record ExtractionCheck(
        int RequirementsFound,
        int PagesWithText,
        int PagesWithoutRequirements,
        int CoveredPercent,
        IReadOnlyList<LeftOverText> LeftOver,
        IReadOnlyList<LeftOverText> ShallSentences,
        IReadOnlyList<string> PossiblyMissing,
        IReadOnlyList<string> FoundTwice,
        IReadOnlyList<string> NotFoundFromContents,
        IReadOnlyList<string>? Codes = null)
    {
        public const int MaxTextLength = 300;
        public const int WarningPercent = 20; // R4

        public static readonly ExtractionCheck Empty = new(0, 0, 0, 100, [], [], [], [], []);

        // R4: the share of the pages with text that gave no requirement
        public int PercentWithoutRequirements => PagesWithText == 0 ? 0 : PagesWithoutRequirements * 100 / PagesWithText;

        public bool NeedsWarning => PercentWithoutRequirements > WarningPercent;

        // R5: for the log entry, e.g. "42 requirements, 3 pages without requirements"
        public string Summary
        {
            get
            {
                var parts = new List<string>
                {
                    $"{RequirementsFound} requirement{(RequirementsFound == 1 ? "" : "s")}",
                    $"{PagesWithoutRequirements} page{(PagesWithoutRequirements == 1 ? "" : "s")} without requirements"
                };
                if (PossiblyMissing.Count > 0)
                {
                    parts.Add($"{PossiblyMissing.Count} possibly missing");
                }

                if (ShallSentences.Count > 0)
                {
                    parts.Add($"{ShallSentences.Count} \"shall\" sentence{(ShallSentences.Count == 1 ? "" : "s")} outside requirements");
                }

                if (NeedsWarning)
                {
                    parts.Add($"warning: {PercentWithoutRequirements} % of the pages gave no requirements");
                }

                return string.Join(", ", parts);
            }
        }

        // R6: what the draft's requirements do not cover yet. Only requirements added by hand count (the extracted ones
        // already hold the text that is listed). A text block or sentence is covered when it starts with such a
        // requirement's code, or when its title or description holds the first words. A missing code is covered when any
        // requirement has it.
        public ExtractionCheck Without(IEnumerable<(string Code, string Title, string? Description)> requirements)
        {
            var all = requirements.ToList();
            var extracted = (Codes ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var list = all.Where(r => !extracted.Contains(r.Code.Trim())).ToList();
            var codes = all.Select(r => r.Code.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool Covered(LeftOverText item)
            {
                var start = item.Text.Length <= 40 ? item.Text.TrimEnd('…') : item.Text[..40];
                return list.Any(r =>
                    item.Text.StartsWith(r.Code.Trim() + " ", StringComparison.OrdinalIgnoreCase)
                    || r.Title.Contains(start, StringComparison.OrdinalIgnoreCase)
                    || (r.Description ?? "").Contains(start, StringComparison.OrdinalIgnoreCase));
            }

            return this with
            {
                LeftOver = LeftOver.Where(l => !Covered(l)).ToList(),
                ShallSentences = ShallSentences.Where(s => !Covered(s)).ToList(),
                PossiblyMissing = PossiblyMissing.Where(m => !codes.Contains(m.Split(' ')[0]) && !codes.Contains(string.Join(' ', m.Split(' ').Take(2)))).ToList(),
                NotFoundFromContents = NotFoundFromContents.Where(c => !codes.Contains(c)).ToList()
            };
        }

        public static string Cut(string text)
        {
            var cleaned = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return cleaned.Length <= MaxTextLength ? cleaned : cleaned[..(MaxTextLength - 1)] + "…";
        }
    }
}
