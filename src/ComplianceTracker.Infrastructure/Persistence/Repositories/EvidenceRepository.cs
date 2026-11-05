using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IEvidenceRepository (US-057). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class EvidenceRepository : IEvidenceRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public EvidenceRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Evidence>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.Evidence
                .Where(e => e.ProjectId == projectId && e.RemovedAt == null)
                .ToListAsync(cancellationToken);

        public Task<Evidence?> GetAsync(Guid projectId, Guid evidenceId, CancellationToken cancellationToken = default)
            => _db.Evidence.FirstOrDefaultAsync(e => e.Id == evidenceId && e.ProjectId == projectId && e.RemovedAt == null, cancellationToken);

        public async Task AddAsync(Evidence evidence, byte[]? content, CancellationToken cancellationToken = default)
        {
            await _db.Evidence.AddAsync(evidence, cancellationToken);
            if (content is not null)
            {
                await _db.EvidenceDocuments.AddAsync(new EvidenceDocument { EvidenceId = evidence.Id, Content = content }, cancellationToken);
            }
        }

        // US-059
        public async Task<IReadOnlyList<EvidenceLink>> ListActiveLinksAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.EvidenceLinks
                .Where(l => l.ProjectId == projectId && l.RemovedAt == null)
                .ToListAsync(cancellationToken);

        public async Task AddLinkAsync(EvidenceLink link, CancellationToken cancellationToken = default)
            => await _db.EvidenceLinks.AddAsync(link, cancellationToken);

        public Task<byte[]?> GetContentAsync(Guid evidenceId, CancellationToken cancellationToken = default)
            => _db.EvidenceDocuments
                .Where(d => d.EvidenceId == evidenceId)
                .Select(d => d.Content)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
