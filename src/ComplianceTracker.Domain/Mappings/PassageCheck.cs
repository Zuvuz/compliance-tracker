using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Mappings
{
    // US-205 R1, R2: what the checking model found when it read a document (or a piece of evidence) against a requirement
    public enum PassageVerdict
    {
        Meets,
        Partly
    }

    // US-205 R2, R3, R10: the verdict, the passage quoted word for word from the document, what is missing for "partly",
    // and the model that checked it. Only "meets" and "partly" become suggestions, so "does not meet" has no value here.
    public sealed record PassageCheck
    {
        public const int MaxPassageLength = 500;
        public const int MaxMissingLength = 500;

        public PassageVerdict Verdict { get; }
        public string Passage { get; }
        public string? Missing { get; }
        public string Model { get; }

        public PassageCheck(PassageVerdict verdict, string passage, string? missing, string model)
        {
            if (!Enum.IsDefined(verdict))
            {
                throw new DomainException("Unknown verdict");
            }

            var quote = (passage ?? "").Trim();
            if (quote.Length == 0)
            {
                throw new DomainException("A checked suggestion needs the passage it is based on");
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new DomainException("A checked suggestion names the model that checked it");
            }

            Verdict = verdict;
            Passage = Cut(quote, MaxPassageLength);
            Missing = verdict == PassageVerdict.Partly && !string.IsNullOrWhiteSpace(missing) ? Cut(missing.Trim(), MaxMissingLength) : null;
            Model = Cut(model.Trim(), 200);
        }

        // R2: "Meets" or "Partly"
        public static string NameOf(PassageVerdict verdict) => verdict == PassageVerdict.Meets ? "Meets" : "Partly";

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..(length - 1)].TrimEnd() + "…";
    }
}
