using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IOrganizationRepository. Users are loaded with the organisation (AutoInclude).
    public class OrganizationRepository : IOrganizationRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public OrganizationRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public Task<bool> CvrExistsAsync(string cvr, CancellationToken cancellationToken = default)
            => _db.Organizations.AnyAsync(o => o.Cvr == cvr, cancellationToken);

        // Only adds the organisation to EF's change tracker. It is saved by IUnitOfWork.SaveChangesAsync.
        public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
            => await _db.Organizations.AddAsync(organization, cancellationToken);

        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _db.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        // The organisation that has a member with this sign-in id (Keycloak "sub")
        public Task<Organization?> GetByMemberAsync(string externalId, CancellationToken cancellationToken = default)
            => _db.Organizations.FirstOrDefaultAsync(o => o.Users.Any(u => u.ExternalId == externalId), cancellationToken);
    }
}
