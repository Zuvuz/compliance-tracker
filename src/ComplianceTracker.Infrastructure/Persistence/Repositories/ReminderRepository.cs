using ComplianceTracker.Application.Reminders;
using ComplianceTracker.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-183
    public class ReminderRepository : IReminderRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ReminderRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Reminder>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Reminders.Where(r => r.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
            => await _db.Reminders.AddAsync(reminder, cancellationToken);
    }
}
