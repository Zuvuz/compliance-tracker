using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Organizations
{
    // Use case: create an organisation (US-001, US-002, US-004).
    // The domain checks the fields; this class checks the rules that need the database
    // and saves the organisation and its log entry together.
    // The creator is always the signed-in user (ICurrentUser), never someone passed in.
    public class OrganizationService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly ICurrentUser _currentUser;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;

        public OrganizationService(
            IOrganizationRepository organizations,
            ICurrentUser currentUser,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork)
        {
            _organizations = organizations;
            _currentUser = currentUser;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        // Creates the organisation with the signed-in user as its Admin
        public async Task<Organization> CreateAsync(
            string name,
            string cvr,
            Sector? sector,
            OrganizationSize? size,
            CancellationToken cancellationToken = default)
        {
            // 1. Validate the fields (throws DomainException with a message for the user)
            var organization = Organization.Create(name, cvr, sector, size, _currentUser.Identity);

            // 2. Check against existing organisations
            if (await _member.FindOrganizationAsync(cancellationToken) is not null)
            {
                // US-004 R4: one organisation per user
                throw new DomainException("User already has an organisation");
            }

            if (await _organizations.CvrExistsAsync(organization.Cvr, cancellationToken))
            {
                throw new DomainException("CVR already in use");
            }

            // 3. Register the organisation and its log entry. There is no member yet,
            //    so the logger is told the organisation and the new Admin directly.
            await _organizations.AddAsync(organization, cancellationToken);
            var admin = organization.Users.Single();
            await _log.LogAsync(organization.Id, admin.Id, ActivityAction.OrganizationCreated, cancellationToken: cancellationToken);

            // 4. Save both at once, so they are either saved together or not at all
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // The user now has an organisation; later lookups in this request must see it
            _member.Reset();
            return organization;
        }
    }
}
