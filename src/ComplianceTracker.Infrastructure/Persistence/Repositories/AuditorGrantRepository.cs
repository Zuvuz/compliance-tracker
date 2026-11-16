using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-182
    public class AuditorGrantRepository : IAuditorGrantRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public AuditorGrantRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<AuditorGrant>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.AuditorGrants.Where(g => g.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public Task<AuditorGrant?> GetByInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
            => _db.AuditorGrants.FirstOrDefaultAsync(g => g.InvitationId == invitationId, cancellationToken);

        public async Task AddAsync(AuditorGrant grant, CancellationToken cancellationToken = default)
            => await _db.AuditorGrants.AddAsync(grant, cancellationToken);
    }
}
