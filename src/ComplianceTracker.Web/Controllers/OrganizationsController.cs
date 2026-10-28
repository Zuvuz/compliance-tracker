using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Models;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ComplianceTracker.Web.Controllers
{
    // Thin controller: it only translates between the form and OrganizationService.
    // All business rules live in the domain and the Application layer (US-003 R6).
    // Requires sign-in through the FallbackPolicy in Program.cs (US-004 R1).
    public class OrganizationsController : Controller
    {
        private readonly OrganizationService _service;
        private readonly IOrganizationRepository _organizations;

        public OrganizationsController(OrganizationService service, IOrganizationRepository organizations)
        {
            _service = service;
            _organizations = organizations;
        }

        // "My organisation": the user's own, or the create page on first sign-in (US-004 R3)
        [HttpGet]
        public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            return organization is null
                ? RedirectToAction(nameof(Create))
                : RedirectToAction(nameof(Details), new { id = organization.Id });
        }

        // GET: shows the empty create form
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // US-004 R4: if the user already has an organisation, show that instead
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is not null)
            {
                return RedirectToAction(nameof(Details), new { id = organization.Id });
            }

            return View(new CreateOrganizationViewModel());
        }

        // POST: creates the organisation from the form
        [HttpPost]
        [ValidateAntiForgeryToken] // US-003 R5: only forms from the app itself (ASVS V3.5.1)
        public async Task<IActionResult> Create(CreateOrganizationViewModel model, CancellationToken cancellationToken)
        {
            // An error here means someone sent a value the form cannot produce (e.g. an unknown sector)
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var organization = await _service.CreateAsync(
                    model.Name ?? "",
                    model.Cvr ?? "",
                    model.Sector,
                    model.Size,
                    User.ToUserIdentity(), // the creator is the signed-in user
                    cancellationToken);

                // US-003 R2: show the new organisation (Post/Redirect/Get, so a reload does not create it again)
                return RedirectToAction(nameof(Details), new { id = organization.Id });
            }
            catch (DomainException ex)
            {
                // US-003 R3: show the domain's reason and keep what the user typed
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            // US-004 R5: only the user's own organisation. Others return "not found",
            // so you cannot even tell whether an organisation with that id exists.
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is null || organization.Id != id)
            {
                return NotFound();
            }

            return View(organization);
        }

        // The organisation the signed-in user belongs to (null if none yet)
        private Task<Organization?> MyOrganizationAsync(CancellationToken cancellationToken)
            => _organizations.GetByMemberAsync(User.ExternalId(), cancellationToken);
    }
}
