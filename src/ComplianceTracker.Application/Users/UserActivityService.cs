using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.Users
{
    // Logger log ind og log ud (US-004 R6, ISO 27001 A.8.15).
    // Første login logges ikke: brugeren har endnu ingen organisation, og oprettelsen logges som OrganizationCreated.
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
