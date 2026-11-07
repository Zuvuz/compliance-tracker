namespace ComplianceTracker.Infrastructure.Persistence
{
    // US-136: the content of an organisation's document, one row per version (by its SHA-256), so listing documents never
    // loads the files and the old versions stay (R3)
    public class OrganisationDocumentFile
    {
        public Guid DocumentId { get; set; }
        public string Sha256 { get; set; } = "";
        public byte[] Content { get; set; } = [];
    }
}
