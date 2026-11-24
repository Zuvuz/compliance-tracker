using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-209
    public class SecurityObjectiveRepository : ISecurityObjectiveRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public SecurityObjectiveRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<SecurityObjective>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.SecurityObjectives.Include(o => o.Measurements).Where(o => o.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<SecurityObjective>> ListTrackedAsync(CancellationToken cancellationToken = default)
            => await _db.SecurityObjectives.Include(o => o.Measurements).Where(o => o.Measure != null && o.RemovedAt == null).ToListAsync(cancellationToken);

        public async Task AddAsync(SecurityObjective objective, CancellationToken cancellationToken = default)
            => await _db.SecurityObjectives.AddAsync(objective, cancellationToken);
    }
}
