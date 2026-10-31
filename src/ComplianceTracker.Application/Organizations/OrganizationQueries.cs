using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Organizations
{
    // What the pages read about organisations (US-029 R7). Only ever the signed-in user's own.
    public class OrganizationQueries
    {
        private readonly CurrentMember _member;

        public OrganizationQueries(CurrentMember member)
        {
            _member = member;
        }

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
