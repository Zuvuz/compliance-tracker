using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Organizations
{
    // What the pages read about organisations (US-029 R7). Only ever the signed-in user's own.
    public class OrganizationQueries
    {
        private readonly CurrentMember _member;
        private readonly ICurrentUser _currentUser;
        private readonly IOrganizationRepository _organizations;

        public OrganizationQueries(CurrentMember member, ICurrentUser currentUser, IOrganizationRepository organizations)
        {
            _member = member;
            _currentUser = currentUser;
            _organizations = organizations;
        }

        // US-090 R3: the organisation the signed-in person was removed from, if they are in none now
        public async Task<string?> FormerOrganizationAsync(CancellationToken cancellationToken = default)
            => await MineAsync(cancellationToken) is null
                ? await _organizations.FormerOrganizationNameAsync(_currentUser.Identity.ExternalId, cancellationToken)
                : null;

        // The user's organisation, or null if they have not created one yet
        public Task<Organization?> MineAsync(CancellationToken cancellationToken = default)
            => _member.FindOrganizationAsync(cancellationToken);

        // The organisation with this id, but only if it is the user's own (US-004 R5).
        // Others return null, so you cannot even tell whether an organisation with that id exists.
        public async Task<Organization?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var mine = await MineAsync(cancellationToken);
            return mine is not null && mine.Id == id ? mine : null;
        }
    }
}
