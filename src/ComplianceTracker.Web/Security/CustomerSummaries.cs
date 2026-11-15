using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;

namespace ComplianceTracker.Web.Security
{
    // US-187 R5: one line per organisation on "My customers"
    public sealed record CustomerSummary(Guid OrganizationId, string Name, string Role, int Projects, int Covered, int Total,
        DateOnly? NextExpiry)
    {
        public int Gaps => Total - Covered;

        public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Covered / Total, MidpointRounding.AwayFromZero);
    }

    // US-187 R5: each organisation is read in its own scope, as if the person had chosen it, so the same queries and the
    // same checks as on the organisation's own pages are used. Lives in Web, because Application never creates scopes.
    public class CustomerSummaries
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly CurrentMember _member;

        public CustomerSummaries(IServiceScopeFactory scopes, CurrentMember member)
        {
            _scopes = scopes;
            _member = member;
        }

        public async Task<IReadOnlyList<CustomerSummary>> ForMeAsync(DateOnly today, CancellationToken cancellationToken = default)
        {
            var result = new List<CustomerSummary>();
            foreach (var membership in await _member.MembershipsAsync(cancellationToken))
            {
                await using var scope = _scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<CurrentPrincipal>().OrganizationId = membership.OrganizationId;
                var projects = await scope.ServiceProvider.GetRequiredService<ProjectQueries>().ListAsync(cancellationToken);
                var compliance = scope.ServiceProvider.GetRequiredService<ComplianceQueries>();
                int covered = 0, total = 0;
                DateOnly? next = null;
                foreach (var project in projects)
                {
                    if (await compliance.ForProjectAsync(project.Id, cancellationToken) is not { } status)
                    {
                        continue;
                    }

                    covered += status.Frameworks.Sum(f => f.Covered);
                    total += status.Frameworks.Sum(f => f.Total);
                    var expiry = status.Frameworks.SelectMany(f => f.Requirements).SelectMany(r => r.Evidence)
                        .Select(e => e.Evidence.ValidUntil).Where(d => d >= today).Min();
                    next = expiry is null ? next : next is null || expiry < next ? expiry : next;
                }

                result.Add(new CustomerSummary(membership.OrganizationId, membership.Name, Domain.Users.RolePermissions.Title(membership.Role),
                    projects.Count, covered, total, next));
            }

            return result;
        }
    }
}
