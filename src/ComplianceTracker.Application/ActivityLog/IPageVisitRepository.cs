using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.ActivityLog
{
    // US-219: the filters of the access log (R5); From and To are UTC times, both included
    public record AccessLogSearch(Guid OrganizationId, Guid? ProjectId, Guid? UserId, VisitKind? Kind, DateTime? From, DateTime? To, int Max);

    // US-219: the visits of the access log. A visit is added and its signs of life saved; it is never changed otherwise and
    // deleted only at the end of the retention period (R9). Like all repositories, IUnitOfWork saves.
    public interface IPageVisitRepository
    {
        Task AddAsync(PageVisit visit, CancellationToken cancellationToken = default);

        Task<PageVisit?> GetAsync(Guid id, CancellationToken cancellationToken = default);

        // R5: newest first, at most Max
        Task<IReadOnlyList<PageVisit>> SearchAsync(AccessLogSearch search, CancellationToken cancellationToken = default);

        // R3: the visits with a sign of life since the time given, of the organisation or one project
        Task<IReadOnlyList<PageVisit>> SeenSinceAsync(Guid organizationId, Guid? projectId, DateTime since, CancellationToken cancellationToken = default);

        // "Seen by": every visit to one item
        Task<IReadOnlyList<PageVisit>> ForItemAsync(Guid organizationId, VisitKind kind, Guid itemId, CancellationToken cancellationToken = default);

        // GDPR art. 15: a member's own visits, newest first
        Task<IReadOnlyList<PageVisit>> ForUserAsync(Guid organizationId, Guid userId, int max, CancellationToken cancellationToken = default);

        // R6: deletes the organisation's visits that started before the time given, and says how many
        Task<int> DeleteStartedBeforeAsync(Guid organizationId, DateTime before, CancellationToken cancellationToken = default);
    }
}
