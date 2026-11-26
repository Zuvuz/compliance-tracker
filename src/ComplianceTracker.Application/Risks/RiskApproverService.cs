using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Risks
{
    // US-217: which roles approve accepting High and Critical risks (R1), changed by the organisation admin only (R7)
    public class RiskApproverService
    {
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;

        public RiskApproverService(CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork)
        {
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        // R7: logged with the old and new roles; requests that wait follow the new setting when they are approved
        public async Task SetAsync(IReadOnlyList<UserRole>? roles, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            var old = organization.SetRiskApproverRoles(roles);
            if (old.SequenceEqual(organization.RiskApproverRoles))
            {
                return;
            }

            await _log.LogAsync(ActivityAction.RiskApproverRolesChanged, organization.Id,
                $"From {Titles(old)} to {Titles(organization.RiskApproverRoles)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // e.g. "Organisation admin, Compliance manager"
        public static string Titles(IEnumerable<UserRole> roles) => string.Join(", ", roles.Select(RolePermissions.Title));
    }
}
