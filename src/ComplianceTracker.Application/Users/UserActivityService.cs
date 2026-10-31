using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.Users
{
    // Logs sign-in and sign-out (US-004 R6, ISO 27001 A.8.15).
    // The first sign-in is not logged: the user has no organisation yet,
    // and creating the organisation is logged as OrganizationCreated instead.
    public class UserActivityService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;

        public UserActivityService(
            IOrganizationRepository organizations,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork)
        {
            _organizations = organizations;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        // Called the moment Keycloak has confirmed the sign-in. The sign-in cookie does not exist yet,
        // so the user cannot come from ICurrentUser and is passed in by the web app.
        public async Task RecordSignInAsync(string externalId, CancellationToken cancellationToken = default)
        {
            var organization = await _organizations.GetByMemberAsync(externalId, cancellationToken);
            if (organization is null)
            {
                return; // see the class comment
            }

            var user = organization.Users.Single(u => u.ExternalId == externalId);
            await _log.LogAsync(organization.Id, user.Id, ActivityAction.UserSignedIn, cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The signed-in user signs out
        public async Task RecordSignOutAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return; // see the class comment
            }

            await _log.LogAsync(ActivityAction.UserSignedOut, cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
