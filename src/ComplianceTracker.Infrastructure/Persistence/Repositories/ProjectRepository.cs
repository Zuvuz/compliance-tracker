using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IProjectRepository (US-008, US-011).
    // Every query filters on the organisation, so other organisations' projects are never returned (R5).
    // The project's frameworks are loaded by AutoInclude (ProjectConfiguration).
    public class ProjectRepository : IProjectRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ProjectRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // R3: "Acme Portal" and "acme portal" count as the same name
        public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? exceptProjectId = null, CancellationToken cancellationToken = default)
            => _db.Projects.AnyAsync(
                p => p.OrganizationId == organizationId && p.Name.ToLower() == name.ToLower()
                    && (exceptProjectId == null || p.Id != exceptProjectId),
                cancellationToken);

        // Only registers the project. Saved by IUnitOfWork, together with the log entry.
        public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
            => await _db.Projects.AddAsync(project, cancellationToken);

        public Task<Project?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default)
            => _db.Projects.FirstOrDefaultAsync(
                p => p.Id == projectId && p.OrganizationId == organizationId,
                cancellationToken);

        public async Task<IReadOnlyList<Project>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Projects
                .Where(p => p.OrganizationId == organizationId)
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Project>> ListUsingFrameworkAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default)
            => await _db.Projects
                .Where(p => p.OrganizationId == organizationId
                    && p.FrameworkHistory.Any(f => f.FrameworkId == frameworkId && f.RemovedAt == null))
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);
    }
}
