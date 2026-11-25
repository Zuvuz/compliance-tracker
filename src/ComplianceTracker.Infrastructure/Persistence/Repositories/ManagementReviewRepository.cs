using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-210
    public class ManagementReviewRepository : IManagementReviewRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ManagementReviewRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ManagementReview>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.ManagementReviews.Include(r => r.Decisions).Where(r => r.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddAsync(ManagementReview review, CancellationToken cancellationToken = default)
            => await _db.ManagementReviews.AddAsync(review, cancellationToken);
    }
}
