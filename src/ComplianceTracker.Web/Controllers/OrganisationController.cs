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

        // GET /Organisation/Profile: US-027 the organisation's details; the Admin can edit them, others see them (R3)
        [HttpGet]
        public async Task<IActionResult> Profile([FromServices] OrganizationQueries organizations,
            [FromServices] Application.Common.CurrentMember member, CancellationToken cancellationToken)
        {
            if (await organizations.MineAsync(cancellationToken) is not { } organization)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(new Models.OrganisationProfileViewModel(organization,
                await member.CanAsync(Domain.Users.Permission.ManageMembers, cancellationToken)));
        }

        // POST /Organisation/Update: US-027 R1, R3, R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Filters.OnDomainError(nameof(Profile))]
        public async Task<IActionResult> Update(string? name, Domain.Organizations.Sector? sector, Domain.Organizations.OrganizationSize? size,
            [FromServices] OrganizationService organizations, CancellationToken cancellationToken)
        {
            var changes = await organizations.UpdateAsync(name, sector, size, cancellationToken);
            TempData["Success"] = changes.Count == 0 ? "Nothing was changed." : "The organisation's details are saved.";
            return RedirectToAction(nameof(Profile));
        }
    }
}
