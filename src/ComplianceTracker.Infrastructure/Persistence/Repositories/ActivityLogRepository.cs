using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IActivityLogRepository. Append-only, like the interface.
    public class ActivityLogRepository : IActivityLogRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ActivityLogRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // Only adds the entry to EF's change tracker. It is saved by IUnitOfWork.SaveChangesAsync.
        public async Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default)
            => await _db.ActivityLog.AddAsync(entry, cancellationToken);

        // The shadow key "Id" is a running number, so it breaks ties between entries with the same time
        public async Task<IReadOnlyList<ActivityLogEntry>> ListAboutAsync(Guid organizationId, Guid subjectId, CancellationToken cancellationToken = default)
            => await _db.ActivityLog
                .AsNoTracking()
                .Where(e => e.OrganizationId == organizationId && e.SubjectId == subjectId)
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => EF.Property<long>(e, "Id"))
                .ToListAsync(cancellationToken);
    }
}
