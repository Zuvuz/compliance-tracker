using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Frameworks
{
    // US-142 R8: a requirement's title and description in another language than English, e.g. Danish ("da").
    // The requirement's own Title and Description are English and the fallback. Built-in texts come as seed data.
    public class RequirementText
    {
        public const int LanguageLength = 2;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid RequirementId { get; private set; }

        // A two-letter ISO 639-1 code in lower case, e.g. "da" or "de"
        public string Language { get; private set; } = "";
        public string Title { get; private set; } = "";
        public string Description { get; private set; } = "";

        // For EF Core only
        private RequirementText() { }

        internal RequirementText(Guid requirementId, string language, string title, string? description)
        {
            RequirementId = requirementId;
            Language = Normalize(language) ?? throw new DomainException("The language must be a two-letter code, e.g. \"da\"");
            if (Language == Requirement.DefaultLanguage)
            {
                throw new DomainException("English is the requirement's own text");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new DomainException("The translated title is required");
            }

            Title = title.Trim();
            Description = description?.Trim() ?? "";
        }

        // "DA", " da-DK " and "da" all become "da"; null when it is not a language code
        public static string? Normalize(string? language)
        {
            var code = (language ?? "").Trim().Split('-', '_')[0].ToLowerInvariant();
            return code.Length == LanguageLength && code.All(char.IsAsciiLetterLower) ? code : null;
        }
    }
}
