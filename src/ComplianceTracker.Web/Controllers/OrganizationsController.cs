using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // Thin controller: it only translates between the pages and the Application layer (US-003 R6, US-029).
    // Use cases change data, queries read it; both work for the signed-in user only.
    // US-045: creating the organisation lives here; its pages are Projects, Users and Settings in the top bar.
    // A broken rule is shown by DomainExceptionFilter, so there is no try/catch here.
    // Requires sign-in through the FallbackPolicy in Program.cs (US-004 R1).
    public class OrganizationsController : Controller
    {
        private readonly OrganizationService _service;
        private readonly OrganizationQueries _organizations;

        public OrganizationsController(OrganizationService service, OrganizationQueries organizations)
        {
            _service = service;
            _organizations = organizations;
        }

        // "My organisation": the Projects page, or the create page on first sign-in (US-004 R3, US-045 R4)
        [HttpGet]
        public async Task<IActionResult> Mine(CancellationToken cancellationToken)
            => await _organizations.MineAsync(cancellationToken) is null
                ? RedirectToAction(nameof(Create))
                : RedirectToAction("Index", "Projects");

        // GET: shows the empty create form
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // US-004 R4: if the user already has an organisation, show that instead
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is not null)
            {
                return RedirectToAction("Index", "Projects");
            }

            // US-090 R3
            ViewData["FormerOrganization"] = await _organizations.FormerOrganizationAsync(cancellationToken);
            return View(new CreateOrganizationViewModel());
        }

        // POST: creates the organisation from the form. The creator is the signed-in user.
        [HttpPost]
        [ValidateAntiForgeryToken] // US-003 R5: only forms from the app itself (ASVS V3.5.1)
        public async Task<IActionResult> Create(CreateOrganizationViewModel model, CancellationToken cancellationToken)
        {
            // An error here means someone sent a value the form cannot produce (e.g. an unknown sector)
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _service.CreateAsync(
                model.Name ?? "",
                model.Cvr ?? "",
                model.Sector,
                model.Size,
                cancellationToken);

            // US-003 R2: show the new organisation's Projects page (Post/Redirect/Get, so a reload does not create it again)
            return RedirectToAction("Index", "Projects");
        }

        // Old address of the organisation page (US-045 R6): now the Projects page
        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
            => await _organizations.GetAsync(id, cancellationToken) is null
                ? NotFound() // US-004 R5: also when the organisation belongs to someone else
                : RedirectToActionPermanent("Index", "Projects");

        // Old address of the Users tab (US-045 R6): now the Users page
        [HttpGet]
        public async Task<IActionResult> Users(Guid id, CancellationToken cancellationToken)
            => await _organizations.GetAsync(id, cancellationToken) is null
                ? NotFound()
                : RedirectToActionPermanent("Index", "Users");
    }
}
