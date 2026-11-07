using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of the US-136 repository. Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class OrganisationDocumentRepository : IOrganisationDocumentRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public OrganisationDocumentRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<OrganisationDocument>> ListAsync(Guid organizationId, bool withRemoved = false, CancellationToken cancellationToken = default)
            => await _db.OrganisationDocuments
                .Include(d => d.Links).Include(d => d.Versions)
                .Where(d => d.OrganizationId == organizationId && (withRemoved || d.RemovedAt == null))
                .OrderBy(d => d.Kind).ThenBy(d => d.Title)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

        public Task<OrganisationDocument?> GetAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken = default)
            => _db.OrganisationDocuments
                .Include(d => d.Links).Include(d => d.Versions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.Id == documentId && d.OrganizationId == organizationId, cancellationToken);

        public async Task AddAsync(OrganisationDocument document, byte[] content, CancellationToken cancellationToken = default)
        {
            await _db.OrganisationDocuments.AddAsync(document, cancellationToken);
            await AddContentAsync(document.Id, document.Sha256, content, cancellationToken);
        }

        public async Task AddContentAsync(Guid documentId, string sha256, byte[] content, CancellationToken cancellationToken = default)
        {
            if (!await _db.OrganisationDocumentFiles.AnyAsync(f => f.DocumentId == documentId && f.Sha256 == sha256, cancellationToken))
            {
                await _db.OrganisationDocumentFiles.AddAsync(new OrganisationDocumentFile { DocumentId = documentId, Sha256 = sha256, Content = content }, cancellationToken);
            }
        }

        public Task<byte[]?> GetContentAsync(Guid documentId, string sha256, CancellationToken cancellationToken = default)
            => _db.OrganisationDocumentFiles
                .Where(f => f.DocumentId == documentId && f.Sha256 == sha256)
                .Select(f => f.Content)
                .FirstOrDefaultAsync(cancellationToken)!;

        public async Task<IReadOnlyList<Evidence>> ListProjectEvidenceAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Evidence
                .Where(e => e.OrganisationDocumentId != null && _db.Projects.Any(p => p.Id == e.ProjectId && p.OrganizationId == organizationId))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<EvidenceLink>> ListProjectLinksAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.EvidenceLinks
                .Where(l => _db.Evidence.Any(e => e.Id == l.EvidenceId && e.OrganisationDocumentId != null
                                                  && _db.Projects.Any(p => p.Id == e.ProjectId && p.OrganizationId == organizationId)))
                .ToListAsync(cancellationToken);
    }
}
