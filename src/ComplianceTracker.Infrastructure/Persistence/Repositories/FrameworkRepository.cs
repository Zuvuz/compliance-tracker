using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    public class FrameworkRepository : IFrameworkRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public FrameworkRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // R4: "ISO/IEC 27001" og "iso/iec 27001" er det samme navn
        public Task<bool> NameExistsAsync(Guid organizationId, string name, CancellationToken cancellationToken = default)
            => _db.Frameworks.AnyAsync(
                f => f.OwnerOrganizationId == organizationId && f.Name.ToLower() == name.ToLower(),
                cancellationToken);

        public async Task AddAsync(Framework framework, CancellationToken cancellationToken = default)
            => await _db.Frameworks.AddAsync(framework, cancellationToken);

        public async Task AddDocumentAsync(Guid frameworkVersionId, byte[] content, CancellationToken cancellationToken = default)
            => await _db.CatalogDocuments.AddAsync(
                new CatalogDocument { FrameworkVersionId = frameworkVersionId, Content = content },
                cancellationToken);

        public Task<Framework?> GetAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default)
            => _db.Frameworks.FirstOrDefaultAsync(
                f => f.Id == frameworkId && f.OwnerOrganizationId == organizationId,
                cancellationToken);

        public async Task<IReadOnlyList<Framework>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Frameworks
                .Where(f => f.OwnerOrganizationId == organizationId)
                .OrderBy(f => f.Name)
                .ToListAsync(cancellationToken);

        // R7: kun hvis versionen hører til et framework i brugerens organisation
        public async Task<(FrameworkVersion Version, byte[] Content)?> GetDocumentAsync(
            Guid organizationId, Guid frameworkVersionId, CancellationToken cancellationToken = default)
        {
            var result = await (
                    from version in _db.FrameworkVersions
                    join framework in _db.Frameworks on version.FrameworkId equals framework.Id
                    join document in _db.CatalogDocuments on version.Id equals document.FrameworkVersionId
                    where version.Id == frameworkVersionId && framework.OwnerOrganizationId == organizationId
                    select new { version, document.Content })
                .FirstOrDefaultAsync(cancellationToken);

            return result is null ? null : (result.version, result.Content);
        }
    }
}