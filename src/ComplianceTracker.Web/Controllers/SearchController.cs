using ComplianceTracker.Application.Search;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-100: the search field in the top bar and the results page, grouped by kind (R3)
    public class SearchController : Controller
    {
        private readonly SearchService _search;

        public SearchController(SearchService search)
        {
            _search = search;
        }

        // GET /Search?q=MFA&more=Risk
        public async Task<IActionResult> Index(string? q, string? more, CancellationToken cancellationToken)
        {
            var moreKind = Enum.TryParse<SearchKind>(more, true, out var k) && Enum.IsDefined(k) ? k : (SearchKind?)null;
            return View(new SearchViewModel(await _search.SearchAsync(q, moreKind, cancellationToken)));
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
