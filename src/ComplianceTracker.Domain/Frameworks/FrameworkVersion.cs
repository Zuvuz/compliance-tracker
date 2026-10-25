namespace ComplianceTracker.Domain.Frameworks
{
    // Én uploadet version af et framework. Ændres aldrig efter upload; en rettelse er en ny version (US-005 R3).
    public class FrameworkVersion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid FrameworkId { get; private set; }
        public int Number { get; private set; }
        public string FileName { get; private set; } = "";
        public long SizeInBytes { get; private set; }
        public string Sha256 { get; private set; } = "";
        public Guid UploadedById { get; private set; }
        public DateTime UploadedAt { get; private set; }

        private FrameworkVersion() { }

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