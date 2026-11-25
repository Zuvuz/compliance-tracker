using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-215
    public class RecurringTaskRepository : IRecurringTaskRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RecurringTaskRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<RecurringTask>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.RecurringTasks.Include(t => t.Completions).Where(t => t.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddAsync(RecurringTask task, CancellationToken cancellationToken = default)
            => await _db.RecurringTasks.AddAsync(task, cancellationToken);
    }
}
