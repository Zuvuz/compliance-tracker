using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IClassificationRepository (US-026). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class ClassificationRepository : IClassificationRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ClassificationRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ProjectClassification>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.ProjectClassifications.Where(c => c.ProjectId == projectId).OrderBy(c => c.ClassifiedAt).ToListAsync(cancellationToken);

        public async Task AddAsync(ProjectClassification classification, CancellationToken cancellationToken = default)
            => await _db.ProjectClassifications.AddAsync(classification, cancellationToken);
    }
}
