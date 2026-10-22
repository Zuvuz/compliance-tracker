using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Organizations
{
    public class OrganizationService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly IActivityLogRepository _activityLog;

        public OrganizationService(IOrganizationRepository organizations, IActivityLogRepository activityLog)
        {
            _organizations = organizations;
            _activityLog = activityLog;
        }

        public Organization Create(string name, string cvr, Sector? sector, OrganizationSize size, string creatorName)
        {
            // 1. Valider felterne
            var organization = Organization.Create(name, cvr, sector, size, creatorName);

            // 2. Tjek mod tidligere organisationer
            if (_organizations.CvrExists(organization.Cvr))
            {
                throw new DomainException("CVR already in use");
            }

            // 3. Gem
            _organizations.Add(organization);

            // 4. Log
            var admin = organization.Users.Single();
            _activityLog.Append(new ActivityLogEntry
            {
                OrganizationId = organization.Id,
                UserId = admin.Id,
                Action = ActivityAction.OrganizationCreated,
                OccurredAt = DateTime.UtcNow
            });

            return organization;
        }
    }
}