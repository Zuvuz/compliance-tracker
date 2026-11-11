using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IGapDismissalRepository (US-119). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class GapDismissalRepository : IGapDismissalRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public GapDismissalRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<GapDismissal>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.GapDismissals.Where(d => d.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task AddAsync(GapDismissal dismissal, CancellationToken cancellationToken = default)
            => _db.GapDismissals.AddAsync(dismissal, cancellationToken).AsTask();
    }
}
