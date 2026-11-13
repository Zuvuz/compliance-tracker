using ComplianceTracker.Application.Audits;
using ComplianceTracker.Domain.Audits;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IAuditRepository (US-134). The organisation check happens before, on the project.
    public class AuditRepository : IAuditRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public AuditRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Audit>> ListAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default)
            => await _db.Audits.Where(a => projectIds.Contains(a.ProjectId))
                .OrderByDescending(a => a.HeldOn).ThenByDescending(a => a.RecordedAt).ToListAsync(cancellationToken);

        public async Task AddAsync(Audit audit, AuditReportFile report, CancellationToken cancellationToken = default)
        {
            await _db.Audits.AddAsync(audit, cancellationToken);
            await _db.AuditReportFiles.AddAsync(report, cancellationToken);
        }

        public Task<AuditReportFile?> GetReportAsync(Guid auditId, CancellationToken cancellationToken = default)
            => _db.AuditReportFiles.FirstOrDefaultAsync(f => f.AuditId == auditId, cancellationToken);
    }
}
