using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ComplianceTracker.Web.Controllers
{
    // Tynd controller: den oversætter kun mellem formularen og OrganizationService.
    // Alle forretningsregler ligger i domænet og Application-laget (US-003 R6).
    // Kræver login via FallbackPolicy i Program.cs (US-004 R1).
    public class OrganizationsController : Controller
    {
        private readonly OrganizationService _service;
        private readonly IOrganizationRepository _organizations;

        public OrganizationsController(OrganizationService service, IOrganizationRepository organizations)
        {
            _service = service;
            _organizations = organizations;
        }

        // "Min organisation": brugerens egen, eller opret-siden ved første login (US-004 R3)
        [HttpGet]
        public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            return organization is null
                ? RedirectToAction(nameof(Create))
                : RedirectToAction(nameof(Details), new { id = organization.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // US-004 R4: har brugeren allerede en organisation, vises den i stedet
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is not null)
            {
                return RedirectToAction(nameof(Details), new { id = organization.Id });
            }

            return View(new CreateOrganizationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // US-003 R5: kun formularer fra applikationen selv (ASVS V3.5.1)
        public async Task<IActionResult> Create(CreateOrganizationViewModel model, CancellationToken cancellationToken)
        {
            // Fejl her betyder, at nogen har sendt en værdi, formularen ikke kan lave (fx en ukendt sektor)
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
                    SignedInUser(),
                    cancellationToken);

                // US-003 R2: vis den nye organisation (Post/Redirect/Get, så en genindlæsning ikke opretter den igen)
                return RedirectToAction(nameof(Details), new { id = organization.Id });
            }
            catch (DomainException ex)
            {
                // US-003 R3: vis domænets begrundelse og behold det indtastede
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            // US-004 R5: kun brugerens egen organisation. Andre giver "ikke fundet",
            // så man ikke kan se, om en organisation med det id overhovedet findes.
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is null || organization.Id != id)
            {
                return NotFound();
            }

            return View(organization);
        }

        private Task<Organization?> MyOrganizationAsync(CancellationToken cancellationToken)
            => _organizations.GetByMemberAsync(SignedInUser().ExternalId, cancellationToken);

        // Den indloggede brugers identitet fra Keycloak (claims i login-cookien)
        private UserIdentity SignedInUser() => new(
            User.FindFirstValue("sub")
                ?? throw new InvalidOperationException("The signed-in user has no 'sub' claim."),
            User.FindFirstValue("name") ?? "",
            User.FindFirstValue("email") ?? "");
    }
}
