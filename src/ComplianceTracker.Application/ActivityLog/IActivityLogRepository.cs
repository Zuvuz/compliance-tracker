using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.ActivityLog
{
    // Kun tilføjelse: loggen kan ikke ændres eller slettes gennem dette interface.
    public interface IActivityLogRepository
    {
        Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default);
    }
}
