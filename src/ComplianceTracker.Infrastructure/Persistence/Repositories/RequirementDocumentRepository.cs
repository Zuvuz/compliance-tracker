using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IRequirementDocumentRepository (US-032). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    // The organisation check happens before (RequirementDocumentService, WorkItemQueries), on the project.
    public class RequirementDocumentRepository : IRequirementDocumentRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RequirementDocumentRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // The versions come along (AutoInclude)
        public async Task<IReadOnlyList<RequirementDocument>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.RequirementDocuments
                .Where(d => d.ProjectId == projectId)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(RequirementDocument document, CancellationToken cancellationToken = default)
            => await _db.RequirementDocuments.AddAsync(document, cancellationToken);

        public async Task AddContentAsync(Guid versionId, byte[] content, CancellationToken cancellationToken = default)
            => await _db.RequirementDocumentFiles.AddAsync(new RequirementDocumentFile { VersionId = versionId, Content = content }, cancellationToken);

        public Task<byte[]?> GetContentAsync(Guid versionId, CancellationToken cancellationToken = default)
            => _db.RequirementDocumentFiles
                .Where(f => f.VersionId == versionId)
                .Select(f => f.Content)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
