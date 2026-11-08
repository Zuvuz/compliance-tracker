using System.Text.RegularExpressions;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // US-007 R1 and US-070: finds requirements in the text of a PDF's pages, and what it did not use. No AI (domain model
    // rule 9). Two kinds of numbering are understood:
    // - standards: a line that starts with a code and a title, e.g. "A.5.15 Access control" or "5.15 Access control"
    // - EU acts (R11): "Article 14" with its paragraphs "1." and points "(a)", and "ANNEX I", "Part II" with "(1)" and "(a)",
    //   giving codes such as "Art. 14(2)(a)" and "Annex I Part II (1)"
    // The lines up to the next code are the requirement's description.
    public static partial class RequirementParser
    {
        // Limits, so a huge or odd document cannot fill the database
        public const int MaxRequirements = 2000;
        public const int MaxTitleLength = 500;
        public const int MaxDescriptionLength = 4000;

        // "A.5.15 Access control", "5.15 Access control", "8.1.2 Title". The title must start with a letter.
        [GeneratedRegex(@"^(?<code>(?:[A-Z]{1,3}\.)?\d{1,3}(?:\.\d{1,3})+)\.?\s+(?<title>\p{L}.*)$")]
        private static partial Regex StandardLine();

        // A table of contents line ends with dot leaders and a page number: "Access control ........ 12"
        [GeneratedRegex(@"\s*(?:\.\s*){3,}\d*\s*$")]
        private static partial Regex DotLeaders();

        // "12", "Page 3", "Page 3 of 18", "- 3 -", "3/18"
        [GeneratedRegex(@"^(?:page\s+)?-?\s*\d{1,4}\s*-?(?:\s*(?:of|/)\s*\d{1,4})?$", RegexOptions.IgnoreCase)]
        private static partial Regex PageNumber();

        [GeneratedRegex(@"^Article\s+(?<number>\d{1,3}[a-z]?)(?:\s+(?<title>\p{Lu}.{0,150}))?$")]
        private static partial Regex ArticleHeading();

        [GeneratedRegex(@"^ANNEX\s+(?<number>[IVXLC]{1,6})\b\s*(?<title>.{0,150})$", RegexOptions.IgnoreCase)]
        private static partial Regex AnnexHeading();

        [GeneratedRegex(@"^Part\s+(?<number>[IVXLC]{1,6})\b\s*(?<title>.{0,150})$", RegexOptions.IgnoreCase)]
        private static partial Regex PartHeading();

        // "1. The manufacturer shall ..." in an article; "(1) ..." or "1. ..." in an annex
        [GeneratedRegex(@"^(?:(?<number>\d{1,3})\.|\((?<number>\d{1,3})\))\s+(?<text>\S.*)$")]
        private static partial Regex Numbered();

        // "(a) protect ..." – a point
        [GeneratedRegex(@"^\((?<letter>[a-z]{1,2})\)\s+(?<text>\S.*)$")]
        private static partial Regex Point();

        [GeneratedRegex(@"[^.!?]*\b(?:shall|must|is required to|are required to)\b[^.!?]*[.!?]?", RegexOptions.IgnoreCase)]
        private static partial Regex ShallSentence();

        // The numbering of a standard's code, e.g. "A.5.15" → ("A.5", 15)
        [GeneratedRegex(@"^(?<parent>.*)\.(?<last>\d{1,3})$")]
        private static partial Regex LastNumber();

        // Start: the line a requirement starts on; Requirement: the rest of its text
        private enum Kind { Furniture, Contents, Preamble, Start, Requirement, LeftOver }

        private sealed class Found
        {
            public required string Code { get; init; }
            public required string Title { get; set; }
            public List<string> Text { get; } = [];
            public bool TitleFromNextLine { get; set; }
        }

        public static ExtractionResult Parse(IReadOnlyList<IReadOnlyList<string>> pages)
        {
            var lines = pages.Select((page, index) => page.Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => (Page: index + 1, Text: l)).ToList()).ToList();
            var furniture = RepeatedLines(lines);
            var euAct = lines.SelectMany(p => p).Any(l => ArticleHeading().IsMatch(l.Text));

            var found = new List<Found>();
            var contentsCodes = new List<string>();
            var classified = new List<(int Page, string Text, Kind Kind)>();

            Found? current = null;
            string? article = null, annex = null, part = null, paragraph = null;
            var seenArticle = false;

            foreach (var (page, text) in lines.SelectMany(p => p))
            {
                if (PageNumber().IsMatch(text) || furniture.Contains(text))
                {
                    classified.Add((page, text, Kind.Furniture));
                    continue;
                }

                if (DotLeaders().IsMatch(text))
                {
                    // R3, R9: a table of contents line; its code must be found later
                    var entry = DotLeaders().Replace(text, "").Trim();
                    if (StandardLine().Match(entry) is { Success: true } m)
                    {
                        contentsCodes.Add(m.Groups["code"].Value);
                    }
                    else if (ArticleHeading().Match(entry) is { Success: true } a)
                    {
                        contentsCodes.Add($"Art. {a.Groups["number"].Value}");
                    }

                    classified.Add((page, text, Kind.Contents));
                    continue;
                }

                Found? started = null;
                if (euAct && ArticleHeading().Match(text) is { Success: true } articleMatch)
                {
                    seenArticle = true;
                    article = articleMatch.Groups["number"].Value;
                    annex = part = paragraph = null;
                    started = new Found
                    {
                        Code = $"Art. {article}",
                        Title = articleMatch.Groups["title"].Success ? articleMatch.Groups["title"].Value.Trim() : "",
                        TitleFromNextLine = !articleMatch.Groups["title"].Success
                    };
                }
                else if (euAct && AnnexHeading().Match(text) is { Success: true } annexMatch && text.StartsWith("ANNEX", StringComparison.Ordinal))
                {
                    seenArticle = true;
                    annex = annexMatch.Groups["number"].Value.ToUpperInvariant();
                    article = part = paragraph = null;
                    started = new Found { Code = $"Annex {annex}", Title = annexMatch.Groups["title"].Value.Trim(), TitleFromNextLine = annexMatch.Groups["title"].Value.Trim().Length == 0 };
                }
                else if (annex is not null && PartHeading().Match(text) is { Success: true } partMatch)
                {
                    part = partMatch.Groups["number"].Value.ToUpperInvariant();
                    paragraph = null;
                    started = new Found { Code = $"Annex {annex} Part {part}", Title = partMatch.Groups["title"].Value.Trim(), TitleFromNextLine = partMatch.Groups["title"].Value.Trim().Length == 0 };
                }
                else if ((article is not null || annex is not null) && Numbered().Match(text) is { Success: true } numbered)
                {
                    paragraph = numbered.Groups["number"].Value;
                    var code = article is not null ? $"Art. {article}({paragraph})" : $"{AnnexCode(annex!, part)} ({paragraph})";
                    started = new Found { Code = code, Title = Short(numbered.Groups["text"].Value) };
                    started.Text.Add(numbered.Groups["text"].Value);
                }
                else if (paragraph is not null && Point().Match(text) is { Success: true } point)
                {
                    var parent = article is not null ? $"Art. {article}({paragraph})" : $"{AnnexCode(annex!, part)} ({paragraph})";
                    started = new Found { Code = $"{parent}({point.Groups["letter"].Value})", Title = Short(point.Groups["text"].Value) };
                    started.Text.Add(point.Groups["text"].Value);
                }
                else if (StandardLine().Match(text) is { Success: true } standard)
                {
                    started = new Found { Code = standard.Groups["code"].Value, Title = standard.Groups["title"].Value.Trim() };
                }

                if (started is not null)
                {
                    current = started;
                    found.Add(started);
                    classified.Add((page, text, Kind.Start));
                    continue;
                }

                if (euAct && !seenArticle)
                {
                    // Recitals and the preamble of an EU act are not requirements (US-137 R3)
                    classified.Add((page, text, Kind.Preamble));
                    continue;
                }

                if (current is null)
                {
                    classified.Add((page, text, Kind.LeftOver));
                    continue;
                }

                if (current.TitleFromNextLine && current.Title.Length == 0)
                {
                    current.Title = text;
                    current.TitleFromNextLine = false;
                }
                else
                {
                    current.Text.Add(text);
                }

                classified.Add((page, text, Kind.Requirement));
            }

            // An article, annex, part or paragraph with smaller parts under it is only a heading; the finest level that holds
            // its own obligation is the requirement (US-137 R2), e.g. "Art. 13(2)(a)" and not "Art. 13(2)"
            var codes = found.Select(f => f.Code).ToList();
            var kept = found
                .Where(f => !(f.Code.StartsWith("Art. ", StringComparison.Ordinal) || f.Code.StartsWith("Annex ", StringComparison.Ordinal))
                            || !codes.Any(c => c.Length > f.Code.Length
                                               && (c.StartsWith(f.Code + "(", StringComparison.Ordinal) || c.StartsWith(f.Code + " ", StringComparison.Ordinal))))
                .ToList();

            // The same code twice usually means a table of contents and then the real text: keep the one with the most text
            var duplicates = kept.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            var requirements = kept
                .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(r => r.Text.Sum(t => t.Length)).First())
                .Take(MaxRequirements)
                .Select(r => new ExtractedRequirement(
                    r.Code,
                    Cut(r.Title.Length > 0 ? r.Title : r.Code, MaxTitleLength),
                    Cut(string.Join(" ", r.Text), MaxDescriptionLength)))
                .ToList();

            return new ExtractionResult(requirements, Check(requirements, classified, contentsCodes, duplicates));
        }

        private static ExtractionCheck Check(
            IReadOnlyList<ExtractedRequirement> requirements, IReadOnlyList<(int Page, string Text, Kind Kind)> classified,
            List<string> contentsCodes, List<string> duplicates)
        {
            var contentPages = classified.Where(l => l.Kind is Kind.Requirement or Kind.Start or Kind.LeftOver).Select(l => l.Page).Distinct().ToList();
            var pagesWithRequirements = classified.Where(l => l.Kind == Kind.Start).Select(l => l.Page).ToHashSet();

            // R1, R2: text on a page where no requirement starts is listed, even when it was added to the requirement
            // before it (it may be a requirement without a number)
            classified = classified
                .Select(l => l.Kind == Kind.Requirement && !pagesWithRequirements.Contains(l.Page) ? (l.Page, l.Text, Kind.LeftOver) : l)
                .ToList();

            // R2: consecutive left-over lines on a page are one block
            var leftOver = new List<LeftOverText>();
            foreach (var group in classified.Where(l => l.Kind == Kind.LeftOver).GroupBy(l => l.Page))
            {
                leftOver.Add(new LeftOverText(group.Key, ExtractionCheck.Cut(string.Join(" ", group.Select(l => l.Text)))));
            }

            // R10
            var shall = classified.Where(l => l.Kind == Kind.LeftOver).GroupBy(l => l.Page)
                .SelectMany(group => ShallSentence().Matches(string.Join(" ", group.Select(l => l.Text)))
                    .Select(m => new LeftOverText(group.Key, ExtractionCheck.Cut(m.Value.Trim()))))
                .Where(s => s.Text.Length > 0)
                .ToList();

            // R7: the share of the text that is part of a requirement or known not to be one
            var total = classified.Sum(l => l.Text.Length);
            var covered = classified.Where(l => l.Kind != Kind.LeftOver).Sum(l => l.Text.Length);

            var codes = requirements.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new ExtractionCheck(
                requirements.Count,
                contentPages.Count,
                contentPages.Count(p => !pagesWithRequirements.Contains(p)),
                total == 0 ? 100 : (int)Math.Floor(covered * 100.0 / total),
                leftOver,
                shall,
                Gaps(requirements.Select(r => r.Code).ToList()),
                duplicates,
                contentsCodes.Distinct(StringComparer.OrdinalIgnoreCase).Where(c => !codes.Contains(c) && !codes.Any(k => k.StartsWith(c + "(", StringComparison.OrdinalIgnoreCase))).ToList(),
                requirements.Select(r => r.Code).ToList());
        }

        // R8: "5.15 may be missing (5.14 is followed by 5.16)", within the same parent, for standards and articles
        public static IReadOnlyList<string> Gaps(IReadOnlyList<string> codes)
        {
            var gaps = new List<string>();
            var numbered = codes
                .Select(c => c.StartsWith("Art. ", StringComparison.Ordinal) && !c.Contains('(') && int.TryParse(c[5..], out var n)
                    ? (Parent: "Art.", Last: n, Code: c, Prefix: "Art. ")
                    : LastNumber().Match(c) is { Success: true } m && int.TryParse(m.Groups["last"].Value, out var last)
                        ? (Parent: m.Groups["parent"].Value, Last: last, Code: c, Prefix: m.Groups["parent"].Value + ".")
                        : (Parent: (string?)null, Last: 0, Code: c, Prefix: ""))
                .Where(x => x.Parent is not null)
                .GroupBy(x => x.Parent);

            foreach (var group in numbered)
            {
                var ordered = group.DistinctBy(x => x.Last).OrderBy(x => x.Last).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    var before = ordered[i - 1];
                    var after = ordered[i];
                    for (var missing = before.Last + 1; missing < after.Last && gaps.Count < 200; missing++)
                    {
                        gaps.Add($"{before.Prefix}{missing} may be missing ({before.Code} is followed by {after.Code})");
                    }
                }
            }

            return gaps;
        }

        // Headers and footers: the same line on at least half of the pages (and at least 3)
        private static HashSet<string> RepeatedLines(List<List<(int Page, string Text)>> pages)
        {
            if (pages.Count < 3)
            {
                return [];
            }

            return pages.SelectMany(p => p.Select(l => l.Text).Distinct())
                .GroupBy(t => t)
                .Where(g => g.Count() >= Math.Max(3, (pages.Count + 1) / 2))
                .Where(g => !StandardLine().IsMatch(g.Key) && !ArticleHeading().IsMatch(g.Key))
                .Select(g => g.Key)
                .ToHashSet();
        }

        private static string AnnexCode(string annex, string? part) => part is null ? $"Annex {annex}" : $"Annex {annex} Part {part}";

        // A point's or paragraph's title: its first words
        private static string Short(string text)
        {
            var cleaned = text.Trim();
            return cleaned.Length <= 120 ? cleaned : cleaned[..117].TrimEnd() + "…";
        }

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }

    public sealed record ExtractionResult(IReadOnlyList<ExtractedRequirement> Requirements, ExtractionCheck Check)
    {
        public static readonly ExtractionResult Empty = new([], ExtractionCheck.Empty);
    }
}
