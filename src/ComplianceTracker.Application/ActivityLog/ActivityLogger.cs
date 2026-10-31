using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.ActivityLog
{
    // The only way use cases write to the activity log (US-029 R6, ISO 27001 A.8.15).
    // It fills in who, where and when, so no use case can forget one of them.
    // Like the repositories it only registers the entry; IUnitOfWork saves it with the change it describes.
    public interface IActivityLogger
    {
        // For the signed-in member: user and organisation come from CurrentMember
        Task LogAsync(ActivityAction action, Guid? subjectId = null, CancellationToken cancellationToken = default);

        // As above, with a text saying what changed, e.g. "Phase: Planning → Development" (US-024 R3)
        Task LogAsync(ActivityAction action, Guid? subjectId, string? details, CancellationToken cancellationToken = default);

        // When there is no signed-in member yet (creating the organisation) or the user is not the
        // current request's user (signing in, before the cookie exists)
        Task LogAsync(Guid organizationId, Guid userId, ActivityAction action, Guid? subjectId = null, CancellationToken cancellationToken = default);
    }

    public class ActivityLogger : IActivityLogger
    {
        private readonly IActivityLogRepository _log;
        private readonly CurrentMember _member;
        private readonly TimeProvider _clock;

        public ActivityLogger(IActivityLogRepository log, CurrentMember member, TimeProvider clock)
        {
            _log = log;
            _member = member;
            _clock = clock;
        }

        public Task LogAsync(ActivityAction action, Guid? subjectId = null, CancellationToken cancellationToken = default)
            => LogAsync(action, subjectId, null, cancellationToken);

        public async Task LogAsync(ActivityAction action, Guid? subjectId, string? details, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            await _log.AppendAsync(
                new ActivityLogEntry(organization.Id, user.Id, action, _clock.GetUtcNow().UtcDateTime, subjectId, details),
                cancellationToken);
        }

        public Task LogAsync(Guid organizationId, Guid userId, ActivityAction action, Guid? subjectId = null, CancellationToken cancellationToken = default)
            => _log.AppendAsync(
                new ActivityLogEntry(organizationId, userId, action, _clock.GetUtcNow().UtcDateTime, subjectId),
                cancellationToken);
    }
}
