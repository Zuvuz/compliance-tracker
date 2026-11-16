using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
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
        public async Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken = default)
            => await _db.Organizations.OrderBy(o => o.Name).ToListAsync(cancellationToken);

        // US-187: one query; the person's own organisation (a role that is not external) first, then by name
        public async Task<IReadOnlyList<Membership>> ListMembershipsAsync(string externalId, CancellationToken cancellationToken = default)
            => await _db.Organizations
                .SelectMany(o => o.Users.Where(u => u.ExternalId == externalId && u.RemovedAt == null)
                    .Select(u => new { o.Id, o.Name, u.Role, u.OnlyProjectId, u.AccessFrom, u.AccessUntil }))
                .OrderBy(m => m.Role == UserRole.Consultant || m.Role == UserRole.Auditor)
                .ThenBy(m => m.Name)
                .Select(m => new Membership(m.Id, m.Name, m.Role) { OnlyProjectId = m.OnlyProjectId, AccessFrom = m.AccessFrom, AccessUntil = m.AccessUntil })
                .ToListAsync(cancellationToken);

        // US-090 R3: the organisation the person was last removed from
        public Task<string?> FormerOrganizationNameAsync(string externalId, CancellationToken cancellationToken = default)
            => _db.Organizations
                .SelectMany(o => o.Users.Where(u => u.ExternalId == externalId && u.RemovedAt != null).Select(u => new { o.Name, u.RemovedAt }))
                .OrderByDescending(x => x.RemovedAt)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
