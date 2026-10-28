using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // Use case: create an organisation (US-001, US-002, US-004).
    // The domain checks the fields; this class checks the rules that need the database
    // and saves the organisation and its log entry together.
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

        // Creates the organisation with the signed-in creator as its Admin
        public async Task<Organization> CreateAsync(
            string name,
            string cvr,
            Sector? sector,
            OrganizationSize size,
            UserIdentity creator,
            CancellationToken cancellationToken = default)
        {
            // 1. Validate the fields (throws DomainException with a message for the user)
            var organization = Organization.Create(name, cvr, sector, size, creator);

            // 2. Check against existing organisations
            if (await _organizations.GetByMemberAsync(creator.ExternalId, cancellationToken) is not null)
            {
                // US-004 R4: one organisation per user
                throw new DomainException("User already has an organisation");
            }

            if (await _organizations.CvrExistsAsync(organization.Cvr, cancellationToken))
            {
                throw new DomainException("CVR already in use");
            }

            // 3. Register the organisation
            await _organizations.AddAsync(organization, cancellationToken);

            // 4. Register the log entry
            var admin = organization.Users.Single();
            await _activityLog.AppendAsync(new ActivityLogEntry(
                organization.Id,
                admin.Id,
                ActivityAction.OrganizationCreated,
                DateTime.UtcNow), cancellationToken);

            // 5. Save both at once, so they are either saved together or not at all
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return organization;
        }
    }
}
