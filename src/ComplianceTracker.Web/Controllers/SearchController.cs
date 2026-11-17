using ComplianceTracker.Application.Search;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-100: the search field in the top bar and the results page, grouped by kind (R3)
    public class SearchController : Controller
    {
        private readonly SearchService _search;
        private readonly Application.Projects.IProjectRepository _projects;
        private readonly Application.Common.CurrentMember _member;

        public SearchController(SearchService search, Application.Projects.IProjectRepository projects, Application.Common.CurrentMember member)
        {
            _search = search;
            _projects = projects;
            _member = member;
        }

        // GET /Search?q=MFA&more=Risk
        public async Task<IActionResult> Index(string? q, string? more, CancellationToken cancellationToken)
        {
            var moreKind = Enum.TryParse<SearchKind>(more, true, out var k) && Enum.IsDefined(k) ? k : (SearchKind?)null;
            var result = await _search.SearchAsync(q, moreKind, cancellationToken);

            // The project names for the hits; only the organisation's own projects can be among them (R2)
            var names = new Dictionary<Guid, string>();
            if (!result.IsEmpty && await _member.FindOrganizationAsync(cancellationToken) is { } organization)
            {
                foreach (var project in await _projects.ListAsync(organization.Id, cancellationToken))
                {
                    names[project.Id] = project.Name;
                }
            }

            return View(new SearchViewModel(result, names));
        }

        // Where a hit opens
        public static string Address(SearchHit hit) => hit.Kind switch
        {
            SearchKind.Requirement => $"/Frameworks/Version/{hit.Where}#req-{hit.Id}",
            SearchKind.WorkItem => $"/Projects/Details/{hit.ProjectId}/{ProjectTabs.ProductRequirements}",
            SearchKind.Evidence => $"/Projects/Details/{hit.ProjectId}/{ProjectTabs.Evidence}",
            SearchKind.Risk => $"/Projects/Details/{hit.ProjectId}/{ProjectTabs.Risks}?risk={hit.Id}",
            _ => $"/Projects/Details/{hit.Id}"
        };
    }
}
