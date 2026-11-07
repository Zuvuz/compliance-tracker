namespace ComplianceTracker.Infrastructure.Persistence
{
    // US-050: the file of a locked report, stored apart so listing them never loads the files. Never changed or deleted.
    public class ReportSnapshotFile
    {
        public Guid SnapshotId { get; set; }
        public byte[] Content { get; set; } = [];
    }
}
