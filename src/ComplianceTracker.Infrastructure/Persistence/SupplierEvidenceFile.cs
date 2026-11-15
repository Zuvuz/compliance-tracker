namespace ComplianceTracker.Infrastructure.Persistence
{
    // US-191: the content of a supplier's evidence file. Its own table, so listing suppliers never loads the files.
    public class SupplierEvidenceFile
    {
        public Guid EvidenceId { get; set; }

        public byte[] Content { get; set; } = [];
    }
}
