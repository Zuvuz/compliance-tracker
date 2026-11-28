using ComplianceTracker.Application.Audits;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Web.Models
{
    // US-229: which pages a tab exists on
    [Flags]
    public enum ProjectTabAreas
    {
        Project = 1,
        OrganisationArea = 2, // US-142
        Both = Project | OrganisationArea
    }

    // US-229: the top of every tab (_ProjectHeader): the project, the organisation, the tab shown and the audits behind the
    // certificate badges (US-134)
    public record ProjectShellView(Project Project, Organization Organization, string Tab, AuditOverview? Audits);

    // US-229: the project page (Details.cshtml): the shell, and the tab's partial with its own model. Partial is null for a
    // tab that still uses ProjectDetailsViewModel (Content), until every tab has its own loader (US-235).
    public record ProjectPage(ProjectShellView Shell, string? Partial, object Content);

    // US-229: what a tab's handler gets: the shell, the address's sub-page and requirement, and the query string
    public record ProjectTabContext(ProjectShellView Shell, string? Sub, string? Req, IQueryCollection Query)
    {
        // Like MVC's model binding of a string: the first value, and an empty one is no value
        public string? Value(string name)
            => Query.TryGetValue(name, out var values) && values.Count > 0 && !string.IsNullOrWhiteSpace(values[0]) ? values[0] : null;
    }

    // US-229: the partial to show and its model
    public record ProjectTabView(string Partial, object Model);

    // US-229: one tab of the project page. It reads its filters from the address, checks its own sub-page, calls its
    // loader (Application) and picks its partial. Null is "not found".
    public interface IProjectTab
    {
        string Slug { get; }
        ProjectTabAreas Areas { get; }
        Task<ProjectTabView?> LoadAsync(ProjectTabContext context, CancellationToken cancellationToken);
    }

    // US-229: the tabs by (slug, area), gathered from every IProjectTab in DI
    public class ProjectTabRegistry
    {
        private readonly IReadOnlyDictionary<(string Slug, ProjectTabAreas Area), IProjectTab> _tabs;

        public ProjectTabRegistry(IEnumerable<IProjectTab> tabs)
        {
            var byKey = new Dictionary<(string, ProjectTabAreas), IProjectTab>();
            foreach (var tab in tabs)
            {
                foreach (var area in new[] { ProjectTabAreas.Project, ProjectTabAreas.OrganisationArea }.Where(a => tab.Areas.HasFlag(a)))
                {
                    if (!byKey.TryAdd((tab.Slug, area), tab))
                    {
                        throw new InvalidOperationException($"Two tabs have the slug '{tab.Slug}' on the {area} page.");
                    }
                }
            }

            _tabs = byKey;
        }

        // Null when the tab has no handler yet; the page then uses the old code (until US-235)
        public IProjectTab? Find(string slug, bool organisationArea)
            => _tabs.GetValueOrDefault((slug, organisationArea ? ProjectTabAreas.OrganisationArea : ProjectTabAreas.Project));
    }
}
