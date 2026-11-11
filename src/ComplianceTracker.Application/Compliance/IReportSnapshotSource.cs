using ComplianceTracker.Domain.Reports;

namespace ComplianceTracker.Application.Compliance
{
    // US-146 R8: a locked report and its file, so it can become evidence. Implemented over IReportSnapshotRepository.
    public interface IReportSnapshotSource
    {
        Task<(ReportSnapshot Snapshot, byte[] Content)?> LockedAsync(Guid organizationId, Guid snapshotId, CancellationToken cancellationToken = default);
    }

    public class ReportSnapshotSource : IReportSnapshotSource
    {
        private readonly Reports.IReportSnapshotRepository _snapshots;

        public ReportSnapshotSource(Reports.IReportSnapshotRepository snapshots)
        {
            _snapshots = snapshots;
        }

        public async Task<(ReportSnapshot Snapshot, byte[] Content)?> LockedAsync(Guid organizationId, Guid snapshotId, CancellationToken cancellationToken = default)
            => await _snapshots.GetAsync(organizationId, snapshotId, cancellationToken) is { } snapshot
               && await _snapshots.GetContentAsync(snapshot.Id, cancellationToken) is { } content
                ? (snapshot, content)
                : null;
    }
}
