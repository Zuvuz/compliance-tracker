using ComplianceTracker.Application.Common;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // Saves everything the repositories have registered. EF's SaveChanges runs all
    // pending changes in one transaction: if one of them fails, none of them are saved.
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ComplianceTrackerDbContext _db;

        public UnitOfWork(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _db.SaveChangesAsync(cancellationToken);
    }
}
