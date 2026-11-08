using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of the US-050 repository. Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class ReportSnapshotRepository : IReportSnapshotRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ReportSnapshotRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ReportSnapshot>> ListAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default)
            => await _db.ReportSnapshots
                .Where(s => s.OrganizationId == organizationId && s.ProjectId == projectId)
                .OrderByDescending(s => s.LockedAt)
                .ToListAsync(cancellationToken);

        public Task<ReportSnapshot?> GetAsync(Guid organizationId, Guid snapshotId, CancellationToken cancellationToken = default)
            => _db.ReportSnapshots.FirstOrDefaultAsync(s => s.Id == snapshotId && s.OrganizationId == organizationId, cancellationToken);

        public async Task<IReadOnlyList<ReportSnapshot>> FindAsync(Guid organizationId, string sha256, CancellationToken cancellationToken = default)
            => await _db.ReportSnapshots.Where(s => s.OrganizationId == organizationId && s.Sha256 == sha256).ToListAsync(cancellationToken);

        public async Task AddAsync(ReportSnapshot snapshot, byte[] content, CancellationToken cancellationToken = default)
        {
            await _db.ReportSnapshots.AddAsync(snapshot, cancellationToken);
            await _db.ReportSnapshotFiles.AddAsync(new ReportSnapshotFile { SnapshotId = snapshot.Id, Content = content }, cancellationToken);
        }

        public Task<byte[]?> GetContentAsync(Guid snapshotId, CancellationToken cancellationToken = default)
            => _db.ReportSnapshotFiles.Where(f => f.SnapshotId == snapshotId).Select(f => f.Content).FirstOrDefaultAsync(cancellationToken)!;
    }
}
