using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-208
    public class IsmsScopeRepository : IIsmsScopeRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public IsmsScopeRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public Task<IsmsScope?> GetAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => _db.IsmsScopes.Include(s => s.Projects).FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);

        public async Task AddAsync(IsmsScope scope, CancellationToken cancellationToken = default)
            => await _db.IsmsScopes.AddAsync(scope, cancellationToken);

        public async Task<IReadOnlyList<InterestedParty>> ListPartiesAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.InterestedParties.Where(p => p.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddPartyAsync(InterestedParty party, CancellationToken cancellationToken = default)
            => await _db.InterestedParties.AddAsync(party, cancellationToken);
    }
}
