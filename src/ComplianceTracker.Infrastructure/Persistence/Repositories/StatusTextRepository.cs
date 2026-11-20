using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of the US-040 repository. Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class StatusTextRepository : IStatusTextRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public StatusTextRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<StatusText>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.StatusTexts.Where(t => t.ProjectId == projectId).ToListAsync(cancellationToken);

        public async Task AddAsync(StatusText text, CancellationToken cancellationToken = default)
            => await _db.StatusTexts.AddAsync(text, cancellationToken);
    }
}
