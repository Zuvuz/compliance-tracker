using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IRequirementScopeDecisionRepository (US-115). Always for one organisation.
    public class RequirementScopeDecisionRepository : IRequirementScopeDecisionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RequirementScopeDecisionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // One query; the newest per requirement is picked in memory (an organisation's decisions are few)
        public async Task<IReadOnlyList<RequirementScopeDecision>> ListNewestAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => (await _db.RequirementScopeDecisions
                    .Where(d => d.OrganizationId == organizationId)
                    .ToListAsync(cancellationToken))
                .GroupBy(d => d.RequirementId)
                .Select(g => g.OrderByDescending(d => d.DecidedAt).First())
                .ToList();

        public async Task AddAsync(RequirementScopeDecision decision, CancellationToken cancellationToken = default)
            => await _db.RequirementScopeDecisions.AddAsync(decision, cancellationToken);
    }
}
