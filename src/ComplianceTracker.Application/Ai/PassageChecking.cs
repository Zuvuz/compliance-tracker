using System.Text;
using System.Text.RegularExpressions;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Ai
{
    // US-205: the check that reads a requirement and the best sections of a document and answers whether the document meets
    // it, with a quote. Embeddings answer "same topic?"; this answers "does it say what the requirement asks for?".
    public static class PassageCheckPrompt
    {
        public const string Version = "passage-check-v1";
        public const string Marker = "Check whether the document";

        public static string For(Requirement requirement, string title, IReadOnlyList<string> sections)
        {
            var text = new StringBuilder();
            text.AppendLine($"{Marker} \"{title}\" meets the regulatory requirement below.");
            text.AppendLine("Use only the document's text. A document that is only about the same topic does not meet it.");
            text.AppendLine("Answer in exactly this shape:");
            text.AppendLine("VERDICT: Meets | Partly meets | Does not meet");
            text.AppendLine("PASSAGE: <one sentence copied word for word from the document that the verdict is based on>");
            text.AppendLine("MISSING: <for \"Partly meets\": what the document does not say; otherwise \"Nothing\">");
            text.AppendLine();
            text.AppendLine($"Requirement {requirement.Code}: {requirement.Title}");
            text.AppendLine(requirement.Description);
            text.AppendLine();
            text.AppendLine($"Document \"{title}\":");
            foreach (var section in sections)
            {
                text.AppendLine(section);
                text.AppendLine("---");
            }

            return text.ToString();
        }

        // A verdict worded the model's own way is still understood; an answer without a verdict is refused (the edge case of
        // a model that answers in a wrong format), and the suggestion is then made as not checked (R7)
        public static (bool? Meets, PassageVerdict? Verdict, string Passage, string? Missing) Parse(string answer)
        {
            string? verdict = null, passage = null, missing = null;
            foreach (var raw in (answer ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim().TrimStart('*', '-', ' ');
                if (Take(line, "VERDICT:") is { } v)
                {
                    verdict = v;
                }
                else if (Take(line, "PASSAGE:") is { } p)
                {
                    passage = p;
                }
                else if (Take(line, "MISSING:") is { } m)
                {
                    missing = m;
                }
            }

            var text = (verdict ?? "").ToLowerInvariant().Trim('.', ' ', '"', '*');
            if (text.Length == 0)
            {
                throw new AiUnavailableException("The AI's answer had no verdict");
            }

            var quote = (passage ?? "").Trim().Trim('"', '“', '”', '\'').Trim();
            var gap = string.IsNullOrWhiteSpace(missing) || missing.Trim().Trim('.').Equals("nothing", StringComparison.OrdinalIgnoreCase) ? null : missing.Trim();
            return text.Contains("not") ? (false, null, quote, null)
                : text.StartsWith("partly") || text.StartsWith("partial") ? (true, PassageVerdict.Partly, quote, gap)
                : text.StartsWith("meet") ? (true, PassageVerdict.Meets, quote, null)
                : throw new AiUnavailableException("The AI's answer had no verdict");

            static string? Take(string line, string label)
                => line.StartsWith(label, StringComparison.OrdinalIgnoreCase) ? line[label.Length..].Trim() : null;
        }
    }

    // US-205 R5: a long document is read in sections, so a requirement met in one section is found. A section is a run of
    // paragraphs of at most MaxLength characters; a paragraph longer than that is split at sentences.
    public static partial class DocumentSections
    {
        public const int MaxLength = 1200;

        // How many sections the check reads for one requirement
        public const int ForCheck = 3;

        [GeneratedRegex(@"\n\s*\n")]
        private static partial Regex Paragraphs();

        [GeneratedRegex(@"(?<=[.!?])\s+")]
        private static partial Regex Sentences();

        public static IReadOnlyList<string> Of(string? text, int maxLength = MaxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            var pieces = Paragraphs().Split(text.Replace("\r\n", "\n"))
                .Select(p => p.Trim()).Where(p => p.Length > 0)
                .SelectMany(p => p.Length <= maxLength ? [p] : Sentences().Split(p).SelectMany(s => Chunks(s.Trim(), maxLength)))
                .ToList();

            var sections = new List<string>();
            var current = new StringBuilder();
            foreach (var piece in pieces)
            {
                if (current.Length > 0 && current.Length + piece.Length + 2 > maxLength)
                {
                    sections.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append("\n\n");
                }

                current.Append(piece);
            }

            if (current.Length > 0)
            {
                sections.Add(current.ToString());
            }

            return sections;
        }

        // A sentence longer than a section is cut, so no section is ever longer than maxLength
        private static IEnumerable<string> Chunks(string text, int maxLength)
        {
            for (var at = 0; at < text.Length; at += maxLength)
            {
                yield return text.Substring(at, Math.Min(maxLength, text.Length - at));
            }
        }
    }

    // US-205 R3: the quote must be in the document word for word, so a model that makes a sentence up is caught. Spaces,
    // line breaks and the case of letters may differ; a quote cut with "…" counts by its first part.
    public static partial class QuoteCheck
    {
        public const int MinimumLength = 12;

        [GeneratedRegex(@"\s+")]
        private static partial Regex Space();

        public static bool IsIn(string? text, string? quote)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(quote))
            {
                return false;
            }

            var wanted = Normal(quote.Trim().TrimEnd('…').TrimEnd('.', ' ').Replace("...", ""));
            return wanted.Length >= MinimumLength && Normal(text).Contains(wanted, StringComparison.Ordinal);
        }

        private static string Normal(string text)
            => Space().Replace(text.Replace('“', '"').Replace('”', '"').Replace('’', '\''), " ").Trim().ToLowerInvariant();
    }

    // The outcome of checking one requirement against a document
    public enum CheckOutcome
    {
        Checked,      // meets or partly: a suggestion, with Check
        NotMet,       // does not meet, or only the same topic (R4): no suggestion
        QuoteMissing, // the quote is not in the document (R3): no suggestion
        Unchecked     // no checking model, no text, or an answer in a wrong format (R7): a suggestion marked "not checked"
    }

    public sealed record CheckResult(CheckOutcome Outcome, PassageCheck? Check = null)
    {
        public bool Suggest => Outcome is CheckOutcome.Checked or CheckOutcome.Unchecked;
    }

    // US-205 R6: the EvidenceCheck task's model (US-117), so the system owner can give the check a larger local model than
    // the other tasks
    public class PassageChecker
    {
        private readonly IRationaleWriter? _writer;

        public PassageChecker(IAiModels models)
        {
            _writer = models.Writer(AiTask.EvidenceCheck);
        }

        public string? Model => _writer?.Name;

        // Reads the requirement with the given sections (the best ones for it); text is the whole document, for the quote
        public async Task<CheckResult> CheckAsync(Requirement requirement, string title, string? text, IReadOnlyList<string> sections,
            CancellationToken cancellationToken = default)
        {
            if (_writer is null || string.IsNullOrWhiteSpace(text) || sections.Count == 0)
            {
                return new CheckResult(CheckOutcome.Unchecked);
            }

            try
            {
                var (meets, verdict, passage, missing) = PassageCheckPrompt.Parse(
                    await _writer.WriteAsync(PassageCheckPrompt.For(requirement, title, sections), cancellationToken));
                if (meets != true || verdict is null)
                {
                    return new CheckResult(CheckOutcome.NotMet);
                }

                return QuoteCheck.IsIn(text, passage)
                    ? new CheckResult(CheckOutcome.Checked, new PassageCheck(verdict.Value, passage, missing, _writer.Name))
                    : new CheckResult(CheckOutcome.QuoteMissing);
            }
            catch (AiUnavailableException)
            {
                return new CheckResult(CheckOutcome.Unchecked);
            }
        }

        // The sections that fit a requirement best, by their vectors (documents) or by shared words when there are none
        public static IReadOnlyList<string> BestSections(IReadOnlyList<string> sections, IReadOnlyList<float[]>? sectionVectors, float[]? requirementVector,
            string requirementText)
        {
            if (sections.Count <= DocumentSections.ForCheck)
            {
                return sections;
            }

            if (sectionVectors is not null && requirementVector is not null)
            {
                return sections.Select((s, i) => (s, Score: Similarity.Cosine(sectionVectors[i], requirementVector)))
                    .OrderByDescending(x => x.Score).Take(DocumentSections.ForCheck).Select(x => x.s).ToList();
            }

            var wanted = Words(requirementText);
            return sections.Select(s => (s, Score: Words(s).Count(wanted.Contains)))
                .OrderByDescending(x => x.Score).Take(DocumentSections.ForCheck).Select(x => x.s).ToList();

            static HashSet<string> Words(string text)
                => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim('.', ',', ';', ':', '(', ')').ToLowerInvariant())
                    .Where(w => w.Length > 3).ToHashSet();
        }
    }
}
