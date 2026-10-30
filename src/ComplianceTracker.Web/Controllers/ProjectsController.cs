using ComplianceTracker.Application.Frameworks;
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
        private readonly IFrameworkRepository _frameworks;
        private readonly IOrganizationRepository _organizations;

        public ProjectsController(
            ProjectService service,
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IOrganizationRepository organizations)
        {
            _service = service;
            _projects = projects;
            _frameworks = frameworks;
            _organizations = organizations;
        }

        // GET /Projects/Create: the empty form, with the organisation's frameworks to tick (US-011 R2)
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // Users without an organisation must create one first
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(new CreateProjectViewModel
            {
                AvailableFrameworks = await _frameworks.ListAsync(organization.Id, cancellationToken)
            });
        }

        // POST /Projects/Create: creates the project from the form (R1)
        [HttpPost]
        [ValidateAntiForgeryToken] // only forms from the app itself (ASVS V3.5.1)
        public async Task<IActionResult> Create(CreateProjectViewModel model, CancellationToken cancellationToken)
        {
            // An error here means someone sent a value the form cannot produce (e.g. an unknown phase)
            if (!ModelState.IsValid)
            {
                return await CreateFormAsync(model, cancellationToken);
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
                    model.FrameworkIds,
                    cancellationToken);

                // Post/Redirect/Get: show the new project, and a reload does not create it again
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }
            catch (DomainException ex)
            {
                // R2, R3: show the rule that was broken and keep what the user typed
                ModelState.AddModelError(string.Empty, ex.Message);
                return await CreateFormAsync(model, cancellationToken);
            }
        }

        // GET /Projects/Details/{id}: the project's own page, with its frameworks (US-011 R6)
        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            var model = await DetailsModelAsync(id, cancellationToken);
            return model is null
                ? NotFound() // R5: also when the project belongs to another organisation
                : View(model);
        }

        // ---------------------------------------------------------- Frameworks (US-011)

        // POST /Projects/AddFramework: R2, R3. Without a version, the newest is used.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AddFramework(Guid projectId, Guid frameworkId, Guid? frameworkVersionId, CancellationToken cancellationToken)
            => ChangeAsync(projectId,
                () => _service.AddFrameworkAsync(User.ExternalId(), projectId, frameworkId, frameworkVersionId, cancellationToken),
                cancellationToken);

        // POST /Projects/UseNewest: R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> UseNewest(Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
            => ChangeAsync(projectId,
                () => _service.UseNewestVersionAsync(User.ExternalId(), projectId, frameworkId, cancellationToken),
                cancellationToken);

        // POST /Projects/RemoveFramework: R7
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> RemoveFramework(Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
            => ChangeAsync(projectId,
                () => _service.RemoveFrameworkAsync(User.ExternalId(), projectId, frameworkId, cancellationToken),
                cancellationToken);

        // Shared by the framework actions: call the use case, then show the project page (with the error, if any)
        private async Task<IActionResult> ChangeAsync(Guid projectId, Func<Task> change, CancellationToken cancellationToken)
        {
            try
            {
                await change();
                return RedirectToAction(nameof(Details), new { id = projectId }); // Post/Redirect/Get
            }
            catch (DomainException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = await DetailsModelAsync(projectId, cancellationToken);
                return model is null ? NotFound() : View(nameof(Details), model);
            }
        }

        // The project with the frameworks the organisation follows, or null if it is not the user's
        private async Task<ProjectDetailsViewModel?> DetailsModelAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            var project = organization is null ? null : await _projects.GetAsync(organization.Id, projectId, cancellationToken);
            return project is null
                ? null
                : new ProjectDetailsViewModel(project, await _frameworks.ListAsync(organization!.Id, cancellationToken));
        }

        // The create form again, with the frameworks to tick
        private async Task<IActionResult> CreateFormAsync(CreateProjectViewModel model, CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            model.AvailableFrameworks = organization is null ? [] : await _frameworks.ListAsync(organization.Id, cancellationToken);
            return View(nameof(Create), model);
        }

        // The organisation the signed-in user belongs to (null if none yet)
        private Task<Organization?> MyOrganizationAsync(CancellationToken cancellationToken)
            => _organizations.GetByMemberAsync(User.ExternalId(), cancellationToken);
    }
}
