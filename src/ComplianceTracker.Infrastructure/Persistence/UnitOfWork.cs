using ComplianceTracker.Domain.ActivityLog;
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
            // US-091: new log entries are chained to their organisation's last entry, in one transaction with a lock per
            // organisation, so two requests at the same moment cannot fork the chain
            var added = _db.ChangeTracker.Entries<ActivityLogEntry>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();
            await using var transaction = added.Count > 0 && _db.Database.CurrentTransaction is null
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;

            foreach (var organization in added.GroupBy(e => e.OrganizationId).OrderBy(g => g.Key))
            {
                var key = organization.Key.ToString("D");
                await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
                var previous = await _db.ActivityLog
                    .Where(e => e.OrganizationId == organization.Key && e.Hash != null)
                    .OrderByDescending(e => EF.Property<long>(e, "Id"))
                    .Select(e => e.Hash)
                    .FirstOrDefaultAsync(cancellationToken);

                foreach (var entry in organization)
                {
                    entry.Seal(previous);
                    previous = entry.Hash;
                }
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // US-092: someone else saved the same row after this request read it; nothing from this attempt is saved
                throw new DomainException(Concurrency.Message);
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

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
    }
}
