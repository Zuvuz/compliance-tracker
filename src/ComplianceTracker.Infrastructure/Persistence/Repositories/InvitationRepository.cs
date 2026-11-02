using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IInvitationRepository (US-009)
    public class InvitationRepository : IInvitationRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public InvitationRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
            => _db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        public Task<Invitation?> GetAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken = default)
            => _db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == organizationId, cancellationToken);

        public async Task<IReadOnlyList<Invitation>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Invitations.Where(i => i.OrganizationId == organizationId).OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);

        public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
            => await _db.Invitations.AddAsync(invitation, cancellationToken);
    }
}
