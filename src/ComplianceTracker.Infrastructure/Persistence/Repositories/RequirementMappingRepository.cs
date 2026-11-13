using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IRequirementMappingRepository (US-037)
    public class RequirementMappingRepository : IRequirementMappingRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RequirementMappingRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<RequirementMapping>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.RequirementMappings.Where(m => m.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddRangeAsync(IEnumerable<RequirementMapping> mappings, CancellationToken cancellationToken = default)
            => await _db.RequirementMappings.AddRangeAsync(mappings, cancellationToken);
    }
}
