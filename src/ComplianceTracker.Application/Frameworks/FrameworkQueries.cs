using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // What the pages read about frameworks (US-029 R7).
    // Every method works on the signed-in user's organisation, so the "only your own data" rule
    // (US-005 R7, ASVS V8.2.2) is kept here and not repeated in each controller.
    // Without an organisation, lists are empty and lookups return null.
    public class FrameworkQueries
    {
        private readonly IFrameworkRepository _frameworks;
        private readonly CurrentMember _member;

        public FrameworkQueries(IFrameworkRepository frameworks, CurrentMember member)
        {
            _frameworks = frameworks;
            _member = member;
        }

        // The organisation's own frameworks and the built-in ones it has switched on (US-006 R3)
        public async Task<IReadOnlyList<Framework>> ListAsync(CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? await _frameworks.ListAsync(organization.Id, cancellationToken)
                : [];

        // One of the organisation's own (uploaded) frameworks, or null
        public async Task<Framework?> GetOwnAsync(Guid frameworkId, CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? await _frameworks.GetAsync(organization.Id, frameworkId, cancellationToken)
                : null;

        // The PDF of one version, if it belongs to the organisation
        public async Task<(FrameworkVersion Version, byte[] Content)?> GetDocumentAsync(Guid frameworkVersionId, CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? await _frameworks.GetDocumentAsync(organization.Id, frameworkVersionId, cancellationToken)
                : null;

        // Every built-in framework, and the ids of those the organisation has switched on (US-006 R2)
        public async Task<(IReadOnlyList<Framework> All, IReadOnlySet<Guid> SwitchedOn)> BuiltInAsync(CancellationToken cancellationToken = default)
        {
            var all = await _frameworks.ListBuiltInAsync(cancellationToken);
            var followed = await ListAsync(cancellationToken);
            return (all, followed.Where(f => f.IsBuiltIn).Select(f => f.Id).ToHashSet());
        }
    }
}
