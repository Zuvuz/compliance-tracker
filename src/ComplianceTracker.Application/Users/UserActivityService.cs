using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.Users
{
    // Logs sign-in and sign-out (US-004 R6, ISO 27001 A.8.15).
    // Called by the web app when Keycloak has confirmed a sign-in, and when the user signs out.
    // The first sign-in is not logged: the user has no organisation yet,
    // and creating the organisation is logged as OrganizationCreated instead.
    public class UserActivityService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly IActivityLogRepository _activityLog;
        private readonly IUnitOfWork _unitOfWork;

        public UserActivityService(
            IOrganizationRepository organizations,
            IActivityLogRepository activityLog,
            IUnitOfWork unitOfWork)
        {
            _organizations = organizations;
            _activityLog = activityLog;
            _unitOfWork = unitOfWork;
        }

        public Task RecordSignInAsync(string externalId, CancellationToken cancellationToken = default)
            => RecordAsync(externalId, ActivityAction.UserSignedIn, cancellationToken);

        public Task RecordSignOutAsync(string externalId, CancellationToken cancellationToken = default)
            => RecordAsync(externalId, ActivityAction.UserSignedOut, cancellationToken);

        private async Task RecordAsync(string externalId, ActivityAction action, CancellationToken cancellationToken)
        {
            // Without an organisation there is nowhere to log the entry (see the class comment)
            var organization = await _organizations.GetByMemberAsync(externalId, cancellationToken);
            if (organization is null)
            {
                return;
            }

            var user = organization.Users.Single(u => u.ExternalId == externalId);
            await _activityLog.AppendAsync(
                new ActivityLogEntry(organization.Id, user.Id, action, DateTime.UtcNow),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
