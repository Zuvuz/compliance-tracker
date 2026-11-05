using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.ActivityLog
{
    // US-091 R3, R5: checks the organisation's chain of log entries, for Admins and Auditors
    public class ActivityLogIntegrityQueries
    {
        private readonly CurrentMember _member;
        private readonly IActivityLogRepository _log;

        public ActivityLogIntegrityQueries(CurrentMember member, IActivityLogRepository log)
        {
            _member = member;
            _log = log;
        }

        public static bool MayCheck(UserRole role) => role is UserRole.Admin or UserRole.Auditor;

        // The result, or null for a member who may not check the log
        public async Task<ChainCheck?> CheckAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return null;
            }

            var (organization, user) = await _member.RequireAsync(cancellationToken);
            if (!MayCheck(user.Role))
            {
                return null;
            }

            return ActivityLogChain.Verify(await _log.ListInOrderWrittenAsync(organization.Id, cancellationToken));
        }
    }
}
