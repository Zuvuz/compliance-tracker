using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects.Tabs;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Web.Models
{
    // US-232: the Compliance tab (US-019, US-041, US-044, US-144) and a requirement's own page under it (US-153), on a
    // project and in the organisation area. A requirement code in the address ("req") is the requirement's page.
    public class ComplianceProjectTab : IProjectTab
    {
        private readonly IProjectTabLoader<ComplianceTabRequest, ComplianceTabDto> _list;
        private readonly IProjectTabLoader<RequirementPageRequest, RequirementPageDto> _page;

        public ComplianceProjectTab(
            IProjectTabLoader<ComplianceTabRequest, ComplianceTabDto> list, IProjectTabLoader<RequirementPageRequest, RequirementPageDto> page)
        {
            _list = list;
            _page = page;
        }

        public string Slug => ProjectTabs.Compliance;
        public ProjectTabAreas Areas => ProjectTabAreas.Both;

        public async Task<ProjectTabView?> LoadAsync(ProjectTabContext context, CancellationToken cancellationToken)
        {
            var address = AddressFrom(context);
            var projectId = context.Shell.Project.Id;
            if (context.Req is { } requirement)
            {
                return await _page.LoadAsync(projectId, new RequirementPageRequest(context.Sub, requirement), cancellationToken) is { } page
                    ? new ProjectTabView("_ComplianceTab", new ComplianceTabViewModel(context.Shell, page.Compliance, address) { Page = page })
                    : null;
            }

            return await _list.LoadAsync(projectId, new ComplianceTabRequest(context.Sub), cancellationToken) is { } list
                ? new ProjectTabView("_ComplianceTab", new ComplianceTabViewModel(context.Shell, list.Compliance, address) { List = list })
                : null;
        }

        // US-044 R2: the filter (an unknown one shows everything) and the search in the address, and the page language
        public static ComplianceAddress AddressFrom(ProjectTabContext context)
            => new(context.Sub, ComplianceFilters.Find(context.Value("show")), context.Value("q"), context.Language);
    }

    // US-232: the Compliance tab's address: the sub-tab, the filter, the search and the language of the texts
    public record ComplianceAddress(string? Sub, string Filter, string? Search, string Language);

    // US-232: the model of _ComplianceTab.cshtml. List is the tab's list of requirements; Page a requirement's page.
    public record ComplianceTabViewModel(ProjectShellView Shell, ProjectCompliance Compliance, ComplianceAddress Address)
    {
        public ComplianceTabDto? List { get; init; }
        public RequirementPageDto? Page { get; init; }

        public Project Project => Shell.Project;
        public Organization Organization => Shell.Organization;
        public string? Sub => Address.Sub;
        public string ComplianceFilter => Address.Filter;
        public string? Search => Address.Search;
        public string Language => Address.Language;
        // US-144 R13 and US-041 R3, also on a requirement's page, whose sub-tab is shown active above it
        public bool ShowsAll => Sub == ComplianceAddresses.AllSub && Compliance.Frameworks.Count > 1;
        public FrameworkCompliance? ActiveCompliance => ComplianceAddresses.Active(Compliance, Sub);
        public Domain.Mappings.NeedsActionReassessment? Reassessment => List?.Reassessment;
        public DateOnly Today => List?.Today ?? default;

        // US-144 R15: the frameworks that can still be added (each at most once), that have an active version (US-007),
        // and of the right level (US-142 R2: organisation-level ones only in the organisation area)
        public IEnumerable<Framework> CanBeAdded
            => (List?.Available ?? []).Where(f => Project.FrameworkInUse(f.Id) is null && f.CurrentVersion(Today) is not null
                && f.Level == (Project.IsOrganisationArea ? FrameworkLevel.Organisation : FrameworkLevel.Product));
    }
}
