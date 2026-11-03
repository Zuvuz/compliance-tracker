namespace ComplianceTracker.Infrastructure.Persistence
{
    // The content of one evidence file (US-057). A storage detail, so it lives here and not in the domain.
    // Its own table, so listing evidence never loads the files. In the database, so backup (US-014) covers it.
    public class EvidenceDocument
    {
        // Same id as the evidence: one file per evidence
        public Guid EvidenceId { get; set; }

        public byte[] Content { get; set; } = [];
    }
}
