using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    public class OrganizationService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly IActivityLogRepository _activityLog;
        private readonly IUnitOfWork _unitOfWork;

        public OrganizationService(
            IOrganizationRepository organizations,
            IActivityLogRepository activityLog,
            IUnitOfWork unitOfWork)
        {
            _organizations = organizations;
            _activityLog = activityLog;
            _unitOfWork = unitOfWork;
        }

        public async Task<Organization> CreateAsync(
            string name,
            string cvr,
            Sector? sector,
            OrganizationSize size,
            UserIdentity creator,
            CancellationToken cancellationToken = default)
        {
            // 1. Valider felterne
            var organization = Organization.Create(name, cvr, sector, size, creator);

            // 2. Tjek mod tidligere organisationer
            if (await _organizations.GetByMemberAsync(creator.ExternalId, cancellationToken) is not null)
            {
                // US-004 R4: én organisation pr. bruger
                throw new DomainException("User already has an organisation");
            }

            if (await _organizations.CvrExistsAsync(organization.Cvr, cancellationToken))
            {
                throw new DomainException("CVR already in use");
            }

            // 3. Registrer organisationen
            await _organizations.AddAsync(organization, cancellationToken);

            // 4. Registrer logposten
            var admin = organization.Users.Single();
            await _activityLog.AppendAsync(new ActivityLogEntry(
                organization.Id,
                admin.Id,
                ActivityAction.OrganizationCreated,
                DateTime.UtcNow), cancellationToken);

            // 5. Gem begge dele samlet, så de enten gemmes sammen eller slet ikke
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return organization;
        }
    }
}
