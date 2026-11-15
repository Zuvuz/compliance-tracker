using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-047
    public class ProjectChecklistRepository : IProjectChecklistRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ProjectChecklistRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ProjectChecklistEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.ProjectChecklistEntries.Where(e => e.ProjectId == projectId).ToListAsync(cancellationToken);

        public async Task AddAsync(ProjectChecklistEntry entry, CancellationToken cancellationToken = default)
            => await _db.ProjectChecklistEntries.AddAsync(entry, cancellationToken);
    }
}
