using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.ActivityLog
{
    // Append-only: the log cannot be changed or deleted through this interface.
    // Like all repositories, it only registers the entry; IUnitOfWork saves it.
    public interface IActivityLogRepository
    {
        Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default);

        // US-022 R5: the organisation's entries about one thing (e.g. a project), newest first
        Task<IReadOnlyList<ActivityLogEntry>> ListAboutAsync(Guid organizationId, Guid subjectId, CancellationToken cancellationToken = default);

        // US-091: every entry of the organisation, in the order they were written, to check the chain
        Task<IReadOnlyList<ActivityLogEntry>> ListInOrderWrittenAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // US-101: one page of the entries that match, and how many match in all. Runs in the database.
        Task<(IReadOnlyList<ActivityLogEntry> Entries, int Total)> SearchAsync(ActivityLogSearch search, CancellationToken cancellationToken = default);
    }
}
