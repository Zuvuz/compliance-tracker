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
        private IReadOnlyList<Organizations.Membership>? _memberships;

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
                // US-187 R2: the chosen organisation, but only where the person is a member; otherwise their own
                var memberships = await MembershipsAsync(cancellationToken);
                var chosen = Choose(memberships, _currentUser.ChosenOrganizationId);
                _organization = chosen is null ? null : await _organizations.GetByIdAsync(chosen.OrganizationId, cancellationToken);
                _looked = true;
            }

            return _organization;
        }

        // US-187: the organisations the signed-in person is a member of, their own first
        public async Task<IReadOnlyList<Organizations.Membership>> MembershipsAsync(CancellationToken cancellationToken = default)
            => _memberships ??= await _organizations.ListMembershipsAsync(_currentUser.Identity.ExternalId, cancellationToken);

        // US-187 R2: a chosen organisation the person is not a member of is ignored, so a changed cookie shows nothing new
        public static Organizations.Membership? Choose(IReadOnlyList<Organizations.Membership> memberships, Guid? chosen)
            => memberships.FirstOrDefault(m => m.OrganizationId == chosen) ?? memberships.FirstOrDefault();

        // The organisation and the user; use cases that change data need both
        public async Task<(Organization Organization, User User)> RequireAsync(CancellationToken cancellationToken = default)
        {
            var organization = await FindOrganizationAsync(cancellationToken)
                ?? throw new DomainException("You must belong to an organisation");
            var user = organization.Members.Single(u => u.ExternalId == _currentUser.Identity.ExternalId);
            return (organization, user);
        }

        // US-010 R5: the organisation and the user, if the user's role allows this. Checked on the server in every use case.
        public async Task<(Organization Organization, User User)> RequireAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await RequireAsync(cancellationToken);
            if (!user.Can(permission))
            {
                throw new DomainException("Your role does not allow this");
            }

            return (organization, user);
        }

        // For pages: whether the signed-in member may do this (false without an organisation)
        public async Task<bool> CanAsync(Permission permission, CancellationToken cancellationToken = default)
            => await FindOrganizationAsync(cancellationToken) is { } organization
               && organization.Members.FirstOrDefault(u => u.ExternalId == _currentUser.Identity.ExternalId)?.Can(permission) == true;

        // Forget the lookup, e.g. right after the user has created their organisation
        public void Reset()
        {
            _looked = false;
            _organization = null;
            _memberships = null;
        }
    }
}
