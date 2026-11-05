using ComplianceTracker.Domain.Common;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Domain.WorkItems
{
    // US-033 R2: the pattern a requirement's ID follows in a Word document, as a regular expression.
    // The default is two to ten capital letters, a hyphen and a number, e.g. "REQ-012" or "SEC-3".
    // A value object: if one exists, it compiles and is short. It only matches at the start of a paragraph (R4).
    public sealed class IdPattern
    {
        public const string Default = @"[A-Z]{2,10}-\d+";

        // US-034 R2: the default for a use case document, e.g. "UC-03"
        public const string UseCaseDefault = @"UC-\d+";
        public const int MaxLength = 200;

        // ASVS V5.1.5: a pattern typed by the user can be slow on purpose; a match gets one second at most
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

        private readonly Regex _start;

        public string Value { get; }

        private IdPattern(string value)
        {
            Value = value;
            // The ID at the start, not followed by more letters or digits, then the rest of the line as the title
            _start = new Regex($@"^\s*(?<id>(?:{value}))(?![\p{{L}}\p{{N}}])[\s:.\-–—]*(?<title>.*)$",
                RegexOptions.CultureInvariant | RegexOptions.Singleline, Timeout);
        }

        // An empty pattern means the default. US-034: for use cases the use case default, also when the form's
        // requirement default was left as it was.
        public static IdPattern Create(string? value, DocumentKind kind = DocumentKind.RequirementSpecification)
        {
            var pattern = string.IsNullOrWhiteSpace(value) ? Default : value.Trim();
            if (kind == DocumentKind.UseCases && pattern == Default)
            {
                pattern = UseCaseDefault;
            }

            if (pattern.Length > MaxLength)
            {
                throw new DomainException($"The ID pattern is longer than {MaxLength} characters");
            }

            try
            {
                return new IdPattern(pattern);
            }
            catch (ArgumentException)
            {
                throw new DomainException("The ID pattern is not a valid regular expression");
            }
        }

        // "REQ-012 The user can log in" → ("REQ-012", "The user can log in"); null if the text does not start with an ID
        public (string Id, string Title)? MatchStart(string text)
        {
            try
            {
                var match = _start.Match(text);
                return match.Success && match.Groups["id"].Length > 0
                    ? (match.Groups["id"].Value.Trim(), match.Groups["title"].Value.Trim())
                    : null;
            }
            catch (RegexMatchTimeoutException)
            {
                throw new DomainException("The ID pattern is too slow. Use a simpler pattern");
            }
        }

        public override string ToString() => Value;
    }
}
