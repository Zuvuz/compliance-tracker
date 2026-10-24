using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // Tynd controller: den oversætter kun mellem formularen og OrganizationService.
    // Alle forretningsregler ligger i domænet og Application-laget (R6).
    public class OrganizationsController : Controller
    {
        private readonly OrganizationService _service;
        private readonly IOrganizationRepository _organizations;

        public OrganizationsController(OrganizationService service, IOrganizationRepository organizations)
        {
            _service = service;
            _organizations = organizations;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateOrganizationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // R5: kun formularer fra applikationen selv (ASVS V3.5.1)
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
                    model.CreatorName ?? "",
                    cancellationToken);

                // R2: vis den nye organisation (Post/Redirect/Get, så en genindlæsning ikke opretter den igen)
                return RedirectToAction(nameof(Details), new { id = organization.Id });
            }
            catch (DomainException ex)
            {
                // R3: vis domænets begrundelse og behold det indtastede
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            var organization = await _organizations.GetByIdAsync(id, cancellationToken);
            if (organization is null)
            {
                return NotFound();
            }

            return View(organization);
        }
    }
}
