using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // Saves everything the repositories have registered. EF's SaveChanges runs all
    // pending changes in one transaction: if one of them fails, none of them are saved.
    public class UnitOfWork : IUnitOfWork
    {
        // US-015: unique indexes whose violation is a normal user mistake, with the message the use case would give.
        // It happens when two people save the same value at the same moment, after both passed the use case's own check.
        private static readonly Dictionary<string, string> UniqueMessages = new(StringComparer.Ordinal)
        {
            ["IX_Organizations_Cvr"] = "CVR already in use"
        };

        private readonly ComplianceTrackerDbContext _db;

        public UnitOfWork(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { } constraint
            } && UniqueMessages.TryGetValue(constraint, out var message))
            {
                // R2: the transaction is rolled back, so nothing from this attempt is saved.
                // R3: other database errors are not caught here and end on the general error page.
                throw new DomainException(message);
            }
        }
    }
}
