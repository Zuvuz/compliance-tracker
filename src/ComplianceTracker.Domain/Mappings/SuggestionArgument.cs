namespace ComplianceTracker.Domain.Mappings
{
    // US-221: the AI suggestions that make a link and so change the coverage; they need the double confirmation (the user,
    // 2026-11-25). The others (checklists, "not applicable", "needs action", security events, document links) keep one.
    public enum SuggestionKind
    {
        Story,
        Evidence,
        EvidenceStory,
        Measure
    }

    // US-221 R3: the AI's argument for a suggestion, in the five fixed parts, built from what the suggestion already has.
    // A missing quote is said plainly. Pure, so the rules are tested on their own.
    public sealed record SuggestionArgument(
        string Asks,
        string? Quote,
        string Missing,
        string Confidence,
        double Score,
        string Models,
        DateTime MadeAt,
        string? Rationale)
    {
        public const string NoQuote = "No quote found: the AI's judgement only";
        public const string ConfirmFirst = "Read the AI's argument and tick that you agree before the link is made";
        public const string NothingMissing = "Nothing missing found";
        public const string NotChecked = "Not checked: no checking model looked for what is missing";
        public const string High = "High";
        public const string Medium = "Medium";
        public const string Low = "Low";

        // Score thresholds for the confidence; a suggestion without a quote is never more than "Low", as it is the AI's
        // judgement only
        public const double HighFrom = 0.75;
        public const double MediumFrom = 0.55;

        public bool HasQuote => Quote is not null;

        // R2: the service refuses an approval without the second confirmation
        public static void RequireConfirmed(bool argumentConfirmed)
        {
            if (!argumentConfirmed)
            {
                throw new Common.DomainException(ConfirmFirst);
            }
        }

        // R6: for the log, e.g. "argument confirmed, confidence High"
        public static string Logged(string confidence) => $"argument confirmed, confidence {confidence}";

        public static string ConfidenceOf(double score, bool hasQuote)
            => !hasQuote ? Low : score >= HighFrom ? High : score >= MediumFrom ? Medium : Low;

        // Part 1: the requirement's code and title as one sentence, e.g. "I(2)(d): Protect against unauthorised access."
        public static string AsksOf(string code, string title)
        {
            var clean = (title ?? "").Trim().TrimEnd('.');
            return clean.Length == 0 ? code : $"{code}: {clean}.";
        }

        public static SuggestionArgument Build(string requirementCode, string requirementTitle, double score, string? quote, PassageVerdict? verdict,
            string? missing, string? rationale, IEnumerable<string?> models, string? promptVersion, DateTime madeAt)
        {
            var cleanQuote = string.IsNullOrWhiteSpace(quote) ? null : quote.Trim();
            var cleanMissing = string.IsNullOrWhiteSpace(missing)
                ? verdict is null ? NotChecked : NothingMissing
                : missing.Trim();
            var names = models.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!.Trim()).Distinct().ToList();
            if (!string.IsNullOrWhiteSpace(promptVersion))
            {
                names.Add($"prompt {promptVersion.Trim()}");
            }

            return new SuggestionArgument(AsksOf(requirementCode, requirementTitle), cleanQuote, cleanMissing, ConfidenceOf(score, cleanQuote is not null),
                Math.Round(score, 2), names.Count == 0 ? "Unknown" : string.Join(", ", names), madeAt,
                string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim());
        }
    }
}
