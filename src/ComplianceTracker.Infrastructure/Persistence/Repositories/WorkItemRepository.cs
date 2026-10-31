using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IWorkItemRepository (US-018).
    // The organisation check happens before (WorkItemQueries, WorkItemSyncService), on the project.
    public class WorkItemRepository : IWorkItemRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public WorkItemRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<WorkItem>> ListAsync(Guid projectId, WorkItemSource source, CancellationToken cancellationToken = default)
            => await _db.WorkItems
                .Where(w => w.ProjectId == projectId && w.Source == source)
                .ToListAsync(cancellationToken);

        // Open ones first, then by title
        public async Task<IReadOnlyList<WorkItem>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => (await _db.WorkItems
                    .Where(w => w.ProjectId == projectId)
                    .ToListAsync(cancellationToken))
                .OrderBy(w => w.State)
                .ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Only registers the work item. Saved by IUnitOfWork, together with the sync's log entry.
        public async Task AddAsync(WorkItem workItem, CancellationToken cancellationToken = default)
            => await _db.WorkItems.AddAsync(workItem, cancellationToken);
    }
}
