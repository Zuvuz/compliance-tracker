using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IFrameworkRepository (US-005, US-006).
    // Every query about uploaded frameworks filters on the organisation, so other organisations'
    // data is never returned (R7). Built-in frameworks have no owner and are shared by everyone.
    public class FrameworkRepository : IFrameworkRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public FrameworkRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // R4: "ISO/IEC 27001" and "iso/iec 27001" count as the same name
        public Task<bool> NameExistsAsync(Guid organizationId, string name, CancellationToken cancellationToken = default)
            => _db.Frameworks.AnyAsync(
                f => f.OwnerOrganizationId == organizationId && f.Name.ToLower() == name.ToLower(),
                cancellationToken);

        // Only registers the framework (with its versions). Saved by IUnitOfWork.
        public async Task AddAsync(Framework framework, CancellationToken cancellationToken = default)
            => await _db.Frameworks.AddAsync(framework, cancellationToken);

        // Only registers the PDF. Saved by IUnitOfWork, together with the version.
        public async Task AddDocumentAsync(Guid frameworkVersionId, byte[] content, CancellationToken cancellationToken = default)
            => await _db.CatalogDocuments.AddAsync(
                new CatalogDocument { FrameworkVersionId = frameworkVersionId, Content = content },
                cancellationToken);

        // Built-in frameworks have no owner, so they never match here (US-006 R5: they cannot be changed)
        public Task<Framework?> GetAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default)
            => _db.Frameworks.FirstOrDefaultAsync(
                f => f.Id == frameworkId && f.OwnerOrganizationId == organizationId,
                cancellationToken);

        // The organisation's own frameworks plus the built-in ones it has switched on,
        // in alphabetical order (versions are included by AutoInclude)
        public async Task<IReadOnlyList<Framework>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Frameworks
                .Where(f => f.OwnerOrganizationId == organizationId
                    || (f.OwnerOrganizationId == null && _db.OrganizationFrameworks.Any(s =>
                        s.OrganizationId == organizationId
                        && s.DisabledAt == null
                        && f.Versions.Any(v => v.Id == s.FrameworkVersionId))))
                .OrderBy(f => f.Name)
                .ToListAsync(cancellationToken);

        // R7: only if the version belongs to a framework in the user's organisation.
        // Joins version → framework (to check the owner) → document (to get the PDF) in one query.
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

        // ---------------------------------------------------------- Built-in frameworks (US-006)

        public async Task<IReadOnlyList<Framework>> ListBuiltInAsync(CancellationToken cancellationToken = default)
            => await _db.Frameworks
                .Where(f => f.OwnerOrganizationId == null)
                .OrderBy(f => f.Name)
                .ToListAsync(cancellationToken);

        public Task<Framework?> GetBuiltInAsync(Guid frameworkId, CancellationToken cancellationToken = default)
            => _db.Frameworks.FirstOrDefaultAsync(
                f => f.Id == frameworkId && f.OwnerOrganizationId == null,
                cancellationToken);

        public Task<OrganizationFramework?> GetActiveSelectionAsync(Guid organizationId, Guid frameworkVersionId, CancellationToken cancellationToken = default)
            => _db.OrganizationFrameworks.FirstOrDefaultAsync(
                s => s.OrganizationId == organizationId && s.FrameworkVersionId == frameworkVersionId && s.DisabledAt == null,
                cancellationToken);

        // Only registers the record. Saved by IUnitOfWork, together with the log entry.
        public async Task AddSelectionAsync(OrganizationFramework selection, CancellationToken cancellationToken = default)
            => await _db.OrganizationFrameworks.AddAsync(selection, cancellationToken);
    }
}
