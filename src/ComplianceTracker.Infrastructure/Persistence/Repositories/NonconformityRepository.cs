using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-212
    public class NonconformityRepository : INonconformityRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public NonconformityRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Nonconformity>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Nonconformities.Where(n => n.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddAsync(Nonconformity nonconformity, CancellationToken cancellationToken = default)
            => await _db.Nonconformities.AddAsync(nonconformity, cancellationToken);
    }
}
