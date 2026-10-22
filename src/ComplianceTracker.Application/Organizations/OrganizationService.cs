using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Organizations
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
            _activityLog.Append(new ActivityLogEntry(
                organization.Id,
                admin.Id,
                ActivityAction.OrganizationCreated,
                DateTime.UtcNow));

            return organization;
        }
    }
}