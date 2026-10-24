using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    public class ActivityLogRepository : IActivityLogRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ActivityLogRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // Tilføjer kun til EF's change tracker. Gemmes først ved IUnitOfWork.SaveChangesAsync.
        public async Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default)
            => await _db.ActivityLog.AddAsync(entry, cancellationToken);
    }
}
