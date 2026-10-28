using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.ActivityLog
{
    // Append-only: the log cannot be changed or deleted through this interface.
    // Like all repositories, it only registers the entry; IUnitOfWork saves it.
    public interface IActivityLogRepository
    {
        Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default);
    }
}
