using ComplianceTracker.Application.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-142 R1: "Organisation" in the top bar. The organisation area is shown with the project page's tabs.
    public class OrganisationController : Controller
    {
        private readonly OrganisationAreaService _area;

        public OrganisationController(OrganisationAreaService area)
        {
            _area = area;
        }

        // GET /Organisation: made the first time it is opened
        public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
        {
            var area = await _area.EnsureAsync(cancellationToken);
            return tab is null
                ? RedirectToAction("Details", "Projects", new { id = area.Id })
                : RedirectToAction("Details", "Projects", new { id = area.Id, tab }); // US-080: e.g. the NIS2 scope check
        }
    }
}
