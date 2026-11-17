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

        // GET /Organisation/Profile: US-027 the organisation's details; US-163 R1 with the industry, the country and whether
        // the products use AI, each with what it changes. The admin edits them, others see them (US-027 R3, US-163 R4).
        // sector: US-163 R2, a sector being chosen; the page suggests its industry without saving anything.
        [HttpGet]
        public async Task<IActionResult> Profile(Domain.Organizations.Sector? sector, [FromServices] OrganizationQueries organizations,
            [FromServices] Application.Common.CurrentMember member, [FromServices] IndustryProfileService industry,
            [FromServices] IndustryProfiles profiles, CancellationToken cancellationToken)
        {
            if (await organizations.MineAsync(cancellationToken) is not { } organization)
            {
                return RedirectToAction("Create", "Organizations");
            }

            var chosen = sector is { } s && Enum.IsDefined(s) ? s : organization.Sector;
            return View(new Models.OrganisationProfileViewModel(organization,
                await member.CanAsync(Domain.Users.Permission.ManageMembers, cancellationToken))
            {
                Industry = await industry.ViewAsync(cancellationToken),
                ChosenSector = chosen,
                // R1: every industry, with a note when it has no suggestions of its own yet
                ComingSoon = Enum.GetValues<Domain.Organizations.Industry>()
                    .Select(i => (i, profiles.For(i, false).ComingSoon))
                    .Where(x => x.ComingSoon is not null)
                    .ToDictionary(x => x.i, x => x.ComingSoon!)
            });
        }

        // POST /Organisation/ChooseIndustry: US-080 R1, R6, on the profile since US-163
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Filters.OnDomainError(nameof(Profile))]
        public async Task<IActionResult> ChooseIndustry(Domain.Organizations.Industry? industry, bool productsUseAi,
            [FromServices] IndustryProfileService profiles, CancellationToken cancellationToken)
        {
            await profiles.ChooseAsync(industry, productsUseAi, cancellationToken);
            TempData["Success"] = "The industry is saved.";
            return RedirectToAction(nameof(Profile), null, null, "industry-profile");
        }

        // POST /Organisation/SetCountry: US-142 R6, on the profile since US-163
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Filters.OnDomainError(nameof(Profile))]
        public async Task<IActionResult> SetCountry(string? country, CancellationToken cancellationToken)
        {
            await _area.SetCountryAsync(country ?? "", cancellationToken);
            TempData["Success"] = "The country is saved.";
            return RedirectToAction(nameof(Profile), null, null, "organisation-country");
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
