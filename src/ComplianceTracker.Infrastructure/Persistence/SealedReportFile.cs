namespace ComplianceTracker.Infrastructure.Persistence
{
    // US-132: the sealed PDF of a signed report, stored apart so listing signings never loads the files
    public class SealedReportFile
    {
        public Guid SigningId { get; set; }
        public byte[] Content { get; set; } = [];
    }
}
