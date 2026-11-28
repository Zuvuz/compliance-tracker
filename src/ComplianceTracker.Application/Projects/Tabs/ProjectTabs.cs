using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects.Tabs
{
    // US-229: one loader per tab of the project page. A loader only reads (the access log is written by the web app);
    // it never sees addresses, slugs or views. Null means the project is not the caller's own, which the page shows as
    // "not found".
    public interface IProjectTabLoader<in TRequest, TResult> where TResult : class
    {
        Task<TResult?> LoadAsync(Guid projectId, TRequest request, CancellationToken cancellationToken = default);
    }

    // US-229: what every tab shows above itself: the project, the organisation (for names) and the audits behind the
    // header's certificate badges (US-134)
    public record ProjectShell(Project Project, Organization Organization, AuditOverview? Audits);

    public class ProjectShellQuery
    {
        private readonly ProjectQueries _projects;
        private readonly OrganizationQueries _organizations;
        private readonly AuditService _audits;

        public ProjectShellQuery(ProjectQueries projects, OrganizationQueries organizations, AuditService audits)
        {
            _projects = projects;
            _organizations = organizations;
            _audits = audits;
        }

        // Null for an unknown project, another organisation's project (US-008 R5) or a member without an organisation
        public async Task<ProjectShell?> LoadAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            return project is null || organization is null
                ? null
                : new ProjectShell(project, organization, await _audits.ForProjectAsync(projectId, cancellationToken));
        }
    }
}
