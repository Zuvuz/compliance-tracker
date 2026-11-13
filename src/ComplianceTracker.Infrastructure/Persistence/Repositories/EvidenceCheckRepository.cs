using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IEvidenceCheckRepository (US-038). The organisation check happens before, on the project.
    public class EvidenceCheckRepository : IEvidenceCheckRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public EvidenceCheckRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<EvidenceCheck>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.EvidenceChecks.Where(c => c.ProjectId == projectId).OrderByDescending(c => c.CheckedAt).ToListAsync(cancellationToken);

        public async Task AddAsync(EvidenceCheck check, CancellationToken cancellationToken = default)
            => await _db.EvidenceChecks.AddAsync(check, cancellationToken);
    }
}
