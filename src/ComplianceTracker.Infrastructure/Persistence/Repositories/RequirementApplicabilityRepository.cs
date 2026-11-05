using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IRequirementApplicabilityRepository (US-043).
    // The organisation check happens before, on the project (ComplianceService, ComplianceQueries).
    public class RequirementApplicabilityRepository : IRequirementApplicabilityRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RequirementApplicabilityRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<RequirementApplicability>> ListActiveAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.RequirementApplicability
                .Where(a => a.ProjectId == projectId && a.RevokedAt == null)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(RequirementApplicability applicability, CancellationToken cancellationToken = default)
            => await _db.RequirementApplicability.AddAsync(applicability, cancellationToken);
    }
}
