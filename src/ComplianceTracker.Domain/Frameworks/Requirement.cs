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

        // The requirement's own number in the framework, e.g. "A.5.15". Unique within the version.
        public string Code { get; private set; } = "";

        public string Title { get; private set; } = "";

        // The text under the title in the PDF (optional)
        public string Description { get; private set; } = "";

        // The order in the document, so the list reads like the PDF
        public int Position { get; private set; }

        // For EF Core only
        private Requirement() { }

        internal Requirement(Guid frameworkVersionId, int position, string code, string title, string? description)
        {
            FrameworkVersionId = frameworkVersionId;
            Position = position;
            Set(code, title, description);
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
    }
}
