using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Web.Security
{
    // For views (@inject in _ViewImports): whether the signed-in member's role allows something (US-010).
    // Only hides buttons; every use case checks the permission on the server as well (R5).
    public class PermissionView
    {
        private readonly CurrentMember _member;

        public PermissionView(CurrentMember member)
        {
            _member = member;
        }

        public Task<bool> CanAsync(Permission permission) => _member.CanAsync(permission);

        // US-102 R6: whether the member may see the whole organisation's activity log (Admins and Auditors)
        public async Task<bool> CanSeeOrganisationLogAsync()
            => await _member.FindOrganizationAsync() is not null
               && Application.ActivityLog.ActivityLogQueries.MaySeeAll((await _member.RequireAsync()).User.Role);
    }
}
