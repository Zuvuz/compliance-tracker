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
    }
}
