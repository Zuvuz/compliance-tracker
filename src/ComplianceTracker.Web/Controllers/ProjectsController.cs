using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Web.Models;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-008: the organisation's projects. Requires sign-in (FallbackPolicy in Program.cs).
    // Thin controller: the rules live in ProjectService and the domain.
    public class ProjectsController : Controller
    {
        private readonly ProjectService _service;
        private readonly IProjectRepository _projects;
        private readonly IOrganizationRepository _organizations;

        public ProjectsController(ProjectService service, IProjectRepository projects, IOrganizationRepository organizations)
        {
            _service = service;
            _projects = projects;
            _organizations = organizations;
        }

        // GET /Projects/Create: the empty form
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // Users without an organisation must create one first
            if (await MyOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(new CreateProjectViewModel());
        }

        // POST /Projects/Create: creates the project from the form (R1)
        [HttpPost]
        [ValidateAntiForgeryToken] // only forms from the app itself (ASVS V3.5.1)
        public async Task<IActionResult> Create(CreateProjectViewModel model, CancellationToken cancellationToken)
        {
            // An error here means someone sent a value the form cannot produce (e.g. an unknown phase)
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var project = await _service.CreateAsync(
                    User.ExternalId(),
                    model.Name ?? "",
                    model.Description,
                    model.IntendedPurpose ?? "",
                    model.Scope,
                    model.LifecyclePhase,
                    cancellationToken);

                // Post/Redirect/Get: show the new project, and a reload does not create it again
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }
            catch (DomainException ex)
            {
                // R2, R3: show the rule that was broken and keep what the user typed
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        // GET /Projects/Details/{id}: the project's own page
        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            var project = organization is null ? null : await _projects.GetAsync(organization.Id, id, cancellationToken);
            if (project is null)
            {
                return NotFound(); // R5: also when the project belongs to another organisation
            }

            return View(project);
        }

        // The organisation the signed-in user belongs to (null if none yet)
        private Task<Organization?> MyOrganizationAsync(CancellationToken cancellationToken)
            => _organizations.GetByMemberAsync(User.ExternalId(), cancellationToken);
    }
}
