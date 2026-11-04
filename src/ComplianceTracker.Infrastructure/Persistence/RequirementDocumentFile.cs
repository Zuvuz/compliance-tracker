namespace ComplianceTracker.Infrastructure.Persistence
{
    // The file of one version of a requirement specification (US-032 R7). A storage detail, so it lives here.
    // Its own table, so listing versions never loads the files. In the database, so backup (US-014) covers it.
    public class RequirementDocumentFile
    {
        // Same id as the version: one file per version
        public Guid VersionId { get; set; }

        public byte[] Content { get; set; } = [];
    }
}
