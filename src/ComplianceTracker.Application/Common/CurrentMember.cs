using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Common
{
    // The signed-in user's organisation and their user in it (US-029 R1).
    // The ONLY place that turns "who is signed in" into "which organisation's data may they see".
    // Scoped: looked up once per request and then remembered.
    public class CurrentMember
    {
        private readonly ICurrentUser _currentUser;
        private readonly IOrganizationRepository _organizations;

        private bool _looked;
        private Organization? _organization;

        public CurrentMember(ICurrentUser currentUser, IOrganizationRepository organizations)
        {
            _currentUser = currentUser;
            _organizations = organizations;
        }

        // The signed-in user's organisation, or null if they have not created one yet
        public async Task<Organization?> FindOrganizationAsync(CancellationToken cancellationToken = default)
        {
            if (!_looked)
            {
                _organization = await _organizations.GetByMemberAsync(_currentUser.Identity.ExternalId, cancellationToken);
                _looked = true;
            }

            return _organization;
        }

        // The organisation and the user; use cases that change data need both
        public async Task<(Organization Organization, User User)> RequireAsync(CancellationToken cancellationToken = default)
        {
            var organization = await FindOrganizationAsync(cancellationToken)
                ?? throw new DomainException("You must belong to an organisation");
            var user = organization.Users.Single(u => u.ExternalId == _currentUser.Identity.ExternalId);
            return (organization, user);
        }

        // Forget the lookup, e.g. right after the user has created their organisation
        public void Reset()
        {
            _looked = false;
            _organization = null;
        }
    }
}
