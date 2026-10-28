namespace ComplianceTracker.Domain.Frameworks
{
    // One uploaded version of a framework. It never changes after upload;
    // a correction is a new version (US-005 R3). The PDF itself is stored separately.
    public class FrameworkVersion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid FrameworkId { get; private set; }

        // 1, 2, 3 ... counted by Framework.AddVersion
        public int Number { get; private set; }

        // The cleaned file name from CatalogFile
        public string FileName { get; private set; } = "";

        public long SizeInBytes { get; private set; }

        // Fingerprint of the PDF, so an auditor can check it has not been changed
        public string Sha256 { get; private set; } = "";

        public Guid UploadedById { get; private set; }

        // Always UTC
        public DateTime UploadedAt { get; private set; }

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
