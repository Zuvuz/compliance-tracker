using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-192
    public class OrganisationChecklistRepository : IOrganisationChecklistRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public OrganisationChecklistRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<OrganisationChecklistItem>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.OrganisationChecklistItems.Where(i => i.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddAsync(OrganisationChecklistItem item, CancellationToken cancellationToken = default)
            => await _db.OrganisationChecklistItems.AddAsync(item, cancellationToken);
    }
}
