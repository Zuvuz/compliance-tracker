using ComplianceTracker.Application.Common;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // EF's SaveChanges kører alle ventende ændringer i én transaktion.
    // Fejler én af dem, bliver ingen af dem gemt.
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
