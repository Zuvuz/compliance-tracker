using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Frameworks
{
    // One requirement in a framework version, e.g. "A.5.15 Access control" (US-007).
    // Found in the PDF or added by hand while the version is a draft; locked when the version is activated.
    // Only FrameworkVersion creates and changes requirements, so the draft rule is always checked.
    public class Requirement
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid FrameworkVersionId { get; private set; }

        // US-092: the row's version, set by the database (PostgreSQL's xmin). A form carries it, so a save
        // can tell that someone else changed the row after the form was opened.
        public uint Version { get; private set; }

        // The requirement's own number in the framework, e.g. "A.5.15". Unique within the version.
        public string Code { get; private set; } = "";

        public string Title { get; private set; } = "";

        // The text under the title in the PDF (optional)
        public string Description { get; private set; } = "";

        // The order in the document, so the list reads like the PDF
        public int Position { get; private set; }

        // US-043: the part of the document it belongs to, e.g. "Part I – Essential cybersecurity requirements".
        // Only set for built-in requirements, which come as seed data; null for requirements found in a PDF.
        public string? Group { get; private set; }

        // US-043: a link to the official text, e.g. the annex on EUR-Lex. Only for built-in requirements.
        public string? Reference { get; private set; }

        // US-143 R4: the requirement it sits under in the standard's structure, e.g. "6.1.2" for "6.1.2 a)"; null at the top
        public Guid? ParentId { get; private set; }

        // US-143 R5: Requirement ("shall"), Recommendation ("should") or Characteristic (a quality model)
        public RequirementKind Kind { get; private set; } = RequirementKind.Requirement;

        // R6: a recommendation never counts in the coverage
        public bool IsRecommendation => Kind == RequirementKind.Recommendation;

        // US-142 R6: in a national law, the code of the directive's requirement it implements, e.g. "Art. 21(2)(a)"
        public string? Implements { get; private set; }

        // US-142 R8: Title and Description are English; other languages are kept here
        public const string DefaultLanguage = "en";
        private readonly List<RequirementText> _texts = new();
        public IReadOnlyList<RequirementText> Texts => _texts;

        // The text in a language, or null when it has none (English always has one)
        public (string Title, string Description)? TextIn(string? language)
        {
            var code = RequirementText.Normalize(language) ?? DefaultLanguage;
            if (code == DefaultLanguage)
            {
                return (Title, Description);
            }

            return _texts.FirstOrDefault(t => t.Language == code) is { } text ? (text.Title, text.Description) : null;
        }

        // R8: the text in the language when there is one, otherwise English, and whether it fell back
        public (string Title, string Description, bool Translated) Show(string? language)
            => TextIn(language) is { } text ? (text.Title, text.Description, true) : (Title, Description, false);

        // Adds or replaces the text in a language
        public void SetText(string language, string title, string? description)
        {
            var text = new RequirementText(Id, language, title, description);
            _texts.RemoveAll(t => t.Language == text.Language);
            _texts.Add(text);
        }

        // For EF Core only
        private Requirement() { }

        internal Requirement(Guid frameworkVersionId, int position, string code, string title, string? description)
        {
            FrameworkVersionId = frameworkVersionId;
            Position = position;
            Set(code, title, description);
        }

        internal void SetParent(Guid? parentId) => ParentId = parentId;

        internal void SetKind(RequirementKind kind)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new DomainException("Choose Requirement, Recommendation or Characteristic");
            }

            Kind = kind;
        }

        internal void Set(string code, string title, string? description)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new DomainException("Requirement code is required");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new DomainException("Requirement title is required");
            }

            Code = code.Trim();
            Title = title.Trim();
            Description = description?.Trim() ?? "";
        }

        // US-110 R2: the same title and text, ignoring differences in whitespace
        public bool HasSameTextAs(Requirement other)
            => Normalized(Title) == Normalized(other.Title) && Normalized(Description) == Normalized(other.Description);

        private static string Normalized(string? text)
            => string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
