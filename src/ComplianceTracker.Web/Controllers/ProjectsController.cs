using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-008, US-011 and US-024: the organisation's projects, their frameworks, editing and lifecycle phase. Requires sign-in (FallbackPolicy in Program.cs).
    // Thin controller: use cases (ProjectService) change data, queries read it. A broken rule is shown
    // by DomainExceptionFilter: forms are shown again, other actions go back to the project page.
    public class ProjectsController : Controller
    {
        private readonly ProjectService _service;
        private readonly ProjectQueries _projects;
        private readonly FrameworkQueries _frameworks;
        private readonly OrganizationQueries _organizations;
        private readonly WorkItemSyncService _sync;
        private readonly WorkItemQueries _workItems;

        public ProjectsController(
            ProjectService service,
            ProjectQueries projects,
            FrameworkQueries frameworks,
            OrganizationQueries organizations,
            WorkItemSyncService sync,
            WorkItemQueries workItems)
        {
            _service = service;
            _projects = projects;
            _frameworks = frameworks;
            _organizations = organizations;
            _sync = sync;
            _workItems = workItems;
        }

        // GET /Projects/Create: the empty form (the framework checkboxes come from the FrameworkChoices view component)
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // Users without an organisation must create one first
            if (await _organizations.MineAsync(cancellationToken) is null)
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

            var project = await _service.CreateAsync(
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

        // GET /Projects/Details/{id}: the project's own page, with its frameworks (US-011 R6)
        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(id, cancellationToken);
            if (project is null)
            {
                return NotFound(); // R5: also when the project belongs to another organisation
            }

            return View(new ProjectDetailsViewModel(
                project,
                await _frameworks.ListAsync(cancellationToken),
                await _workItems.ListAsync(id, cancellationToken) ?? []));
        }

        // ---------------------------------------------------------- User stories from GitHub (US-018)

        // GET /Projects/WorkItems/{id}: only the work items section, fetched again by the page when SignalR says it changed (R7)
        [HttpGet]
        public async Task<IActionResult> WorkItems(Guid id, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(id, cancellationToken);
            var workItems = await _workItems.ListAsync(id, cancellationToken);
            if (project is null || workItems is null)
            {
                return NotFound(); // R9
            }

            return PartialView("_WorkItems", new WorkItemsViewModel(project, workItems));
        }

        // POST /Projects/LinkGitHub: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> LinkGitHub(Guid projectId, string? repository, string? label, CancellationToken cancellationToken)
        {
            await _sync.LinkGitHubAsync(projectId, repository ?? "", label, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST /Projects/SyncNow: R5. A failed sync is shown on the page from the project's GitHub link.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> SyncNow(Guid projectId, CancellationToken cancellationToken)
        {
            await _sync.SyncNowAsync(projectId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // ---------------------------------------------------------- Editing (US-024)

        // GET /Projects/Edit/{id}: the form, filled in with the project's details
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(id, cancellationToken);
            if (project is null)
            {
                return NotFound();
            }

            // R4: a project at end of support is only shown, so the user goes back to its page with the reason
            if (project.IsClosed)
            {
                TempData[DomainExceptionFilter.TempDataKey] = "A project at end of support cannot be changed";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(EditProjectViewModel.From(project));
        }

        // POST /Projects/Edit: R1, R3. A broken rule shows the form again with what was typed (IFormModel).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProjectViewModel model, CancellationToken cancellationToken)
        {
            await _service.UpdateAsync(
                model.Id, model.Name ?? "", model.Description, model.IntendedPurpose ?? "", model.Scope, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = model.Id }); // Post/Redirect/Get
        }

        // POST /Projects/ChangePhase: R2, R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> ChangePhase(Guid projectId, LifecyclePhase phase, CancellationToken cancellationToken)
        {
            await _service.ChangePhaseAsync(projectId, phase, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // ---------------------------------------------------------- Frameworks (US-011)

        // POST /Projects/AddFramework: R2, R3. Without a version, the newest is used.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> AddFramework(Guid projectId, Guid frameworkId, Guid? frameworkVersionId, CancellationToken cancellationToken)
        {
            await _service.AddFrameworkAsync(projectId, frameworkId, frameworkVersionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId }); // Post/Redirect/Get
        }

        // POST /Projects/UseNewest: R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> UseNewest(Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
        {
            await _service.UseNewestVersionAsync(projectId, frameworkId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST /Projects/RemoveFramework: R7
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> RemoveFramework(Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
        {
            await _service.RemoveFrameworkAsync(projectId, frameworkId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }
    }
}
