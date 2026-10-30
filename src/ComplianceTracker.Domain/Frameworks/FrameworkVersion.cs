namespace ComplianceTracker.Domain.Frameworks
{
    // One version of a framework. It never changes; a correction is a new version (US-005 R3).
    // For an uploaded framework it is a PDF (stored separately); for a built-in framework
    // it is the published regulation, found through SourceReference (US-006).
    public class FrameworkVersion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid FrameworkId { get; private set; }

        // 1, 2, 3 ... counted by Framework.AddVersion
        public int Number { get; private set; }

        // The cleaned file name from CatalogFile. Empty for built-in frameworks.
        public string FileName { get; private set; } = "";

        // 0 for built-in frameworks
        public long SizeInBytes { get; private set; }

        // Fingerprint of the PDF, so an auditor can check it has not been changed. Empty for built-in frameworks.
        public string Sha256 { get; private set; } = "";

        // The member who uploaded it. Null for built-in frameworks, which come with the tracker.
        public Guid? UploadedById { get; private set; }

        // When it was uploaded, or for built-in frameworks when the regulation was published. Always UTC.
        public DateTime UploadedAt { get; private set; }

        // Where the official text is published, e.g. a EUR-Lex address. Only for built-in frameworks (US-006 R6).
        public string? SourceReference { get; private set; }

        // For EF Core only
        private FrameworkVersion() { }

        // Internal: only Framework can create versions, so the numbering is always right
        internal FrameworkVersion(Guid frameworkId, int number, CatalogFile file, Guid uploadedById, DateTime uploadedAt)
        {
            FrameworkId = frameworkId;
            Number = number;
            FileName = file.FileName;
            SizeInBytes = file.Content.Length;
            Sha256 = file.Sha256;
            UploadedById = uploadedById;
            UploadedAt = uploadedAt;
        }
    }
}
