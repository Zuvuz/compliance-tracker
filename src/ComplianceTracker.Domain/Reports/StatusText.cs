using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Reports
{
    // US-040: one text part of a project's management report: the summary, or the short text of one regulation. The AI
    // writes it from the tracker's figures only (R3); it is marked AI-generated until a person accepts or edits it (R4,
    // AI Act Art. 50). Basis is a fingerprint of the figures it was written or accepted for, so a text the figures have
    // moved away from is known as outdated and must be drafted or accepted again before the report is locked.
    public class StatusText
    {
        public const int MaxLength = 1500;
        public const string Summary = "summary";

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }

        // "summary", or the regulation's name (Framework.Name)
        public string Part { get; private set; } = "";
        public string Text { get; private set; } = "";
        public bool IsAiGenerated { get; private set; }
        public string Basis { get; private set; } = "";

        // The model that wrote it last
        public string? Model { get; private set; }
        public DateTime WrittenAt { get; private set; }

        // Who accepted or edited it last, and when
        public Guid? ChangedById { get; private set; }
        public DateTime? ChangedAt { get; private set; }

        // For EF Core only
        private StatusText() { }

        public bool IsSummary => Part == Summary;

        public static StatusText Draft(Guid projectId, string part, string text, string basis, string model, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                throw new DomainException("A text needs a part");
            }

            var draft = new StatusText { ProjectId = projectId, Part = part.Trim() };
            draft.Redraft(text, basis, model, now);
            return draft;
        }

        // R5: drafted again; AI-generated again, whatever it was
        public void Redraft(string text, string basis, string model, DateTime now)
        {
            Text = Clean(text);
            Basis = basis;
            Model = model;
            WrittenAt = now;
            IsAiGenerated = true;
            ChangedById = null;
            ChangedAt = null;
        }

        // R4: a person takes the text as it is, for today's figures
        public void Accept(Guid userId, string basis, DateTime now)
        {
            if (!IsAiGenerated && !IsOutdated(basis))
            {
                throw new DomainException("The text is accepted already");
            }

            IsAiGenerated = false;
            Basis = basis;
            ChangedById = userId;
            ChangedAt = now;
        }

        // R4: a person's own words; no longer AI-generated
        public void Edit(string text, Guid userId, string basis, DateTime now)
        {
            Text = Clean(text);
            IsAiGenerated = false;
            Basis = basis;
            ChangedById = userId;
            ChangedAt = now;
        }

        public bool IsOutdated(string currentBasis) => Basis != currentBasis;

        private static string Clean(string? text)
        {
            var cleaned = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (cleaned.Length == 0)
            {
                throw new DomainException("Write a text");
            }

            if (cleaned.Length > MaxLength)
            {
                throw new DomainException($"A text can be at most {MaxLength} characters");
            }

            return cleaned;
        }
    }
}
