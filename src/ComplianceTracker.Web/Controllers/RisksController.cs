using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-042: a project's risks. The register is the project page's Risks tab; this controller has the form
    // and each risk's own page. Thin, like the others: RiskService changes data, the queries read it.
    public class RisksController : Controller
    {
        private readonly RiskService _service;
        private readonly RiskQueries _risks;
        private readonly ProjectQueries _projects;
        private readonly OrganizationQueries _organizations;
        private readonly ComplianceQueries _compliance;
        private readonly TimeProvider _clock;

        public RisksController(
            RiskService service,
            RiskQueries risks,
            ProjectQueries projects,
            OrganizationQueries organizations,
            ComplianceQueries compliance,
            TimeProvider clock)
        {
            _service = service;
            _risks = risks;
            _projects = projects;
            _organizations = organizations;
            _compliance = compliance;
            _clock = clock;
        }

        // GET /Risks/Add?projectId=...: the empty form; the signed-in member is the default owner
        [HttpGet]
        public async Task<IActionResult> Add(Guid projectId, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            if (project is null || organization is null)
            {
                return NotFound();
            }

            var me = organization.Users.FirstOrDefault(u => u.ExternalId == User.FindFirst("sub")?.Value);
            return View(new RiskFormModel { ProjectId = project.Id, ProjectName = project.Name, OwnerId = me?.Id ?? Guid.Empty });
        }

        // POST /Risks/Add: R1–R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(RiskFormModel model, CancellationToken cancellationToken)
        {
            await _service.AddAsync(model.ProjectId, Input(model), cancellationToken);
            return RedirectToAction("Details", "Projects", new { id = model.ProjectId, tab = ProjectTabs.Risks });
        }

        // GET /Risks/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
        {
            var risk = await _risks.GetAsync(id, cancellationToken);
            var project = risk is null ? null : await _projects.GetAsync(risk.ProjectId, cancellationToken);
            if (risk is null || project is null)
            {
                return NotFound();
            }

            return View(RiskFormModel.From(risk, project.Name));
        }

        // POST /Risks/Edit: R9 (old and new values are logged)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RiskFormModel model, CancellationToken cancellationToken)
        {
            await _service.UpdateAsync(model.ProjectId, model.RiskId ?? Guid.Empty, Input(model), cancellationToken, model.Version);
            return RedirectToAction(nameof(Details), new { id = model.RiskId });
        }

        // GET /Risks/Details/{id}: the risk, its measures, and accept / close / review (R11: only the organisation's)
        [HttpGet]
        public async Task<IActionResult> Details(Guid id, [FromServices] EvidenceQueries evidence, CancellationToken cancellationToken)
        {
            var risk = await _risks.GetAsync(id, cancellationToken);
            var project = risk is null ? null : await _projects.GetAsync(risk.ProjectId, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            if (risk is null || project is null || organization is null)
            {
                return NotFound();
            }

            return View(new RiskPageViewModel(risk, project, organization,
                await _compliance.ForProjectAsync(project.Id, cancellationToken), _clock.GetUtcNow().UtcDateTime)
            {
                // US-111 R2: the project's evidence, to link to an action
                ProjectEvidence = (await evidence.ForProjectAsync(project.Id, cancellationToken))?.Evidence.Select(e => e.Evidence).ToList() ?? []
            });
        }

        // POST /Risks/Accept: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> Accept(Guid id, string? reason, CancellationToken cancellationToken)
        {
            await _service.AcceptAsync(await ProjectOfAsync(id, cancellationToken), id, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Risks/Close: R7
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> Close(Guid id, string? reason, CancellationToken cancellationToken)
        {
            await _service.CloseAsync(await ProjectOfAsync(id, cancellationToken), id, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Risks/Review: R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken)
        {
            await _service.MarkReviewedAsync(await ProjectOfAsync(id, cancellationToken), id, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Risks/AddMeasure: R5. "measure" is "requirement:{id}" or "story:{id}".
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> AddMeasure(Guid id, string? measure, CancellationToken cancellationToken)
        {
            var (kind, value) = (measure ?? "").Split(':') is [var k, var v] && Guid.TryParse(v, out var parsed)
                ? (k, parsed)
                : throw new DomainException("Choose a requirement or a user story");

            await _service.AddMeasureAsync(await ProjectOfAsync(id, cancellationToken), id,
                kind == "requirement" ? value : null, kind == "story" ? value : null, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Risks/RemoveMeasure
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> RemoveMeasure(Guid id, Guid measureId, CancellationToken cancellationToken)
        {
            await _service.RemoveMeasureAsync(await ProjectOfAsync(id, cancellationToken), id, measureId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------------------------------------------------------- Treatment actions (US-111)

        // POST /Risks/AddAction: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> AddAction(Guid id, string? title, Guid? ownerId, DateOnly? dueOn, CancellationToken cancellationToken)
        {
            await _service.AddActionAsync(await ProjectOfAsync(id, cancellationToken), id, title ?? "",
                ownerId ?? throw new DomainException("Choose an owner"), dueOn, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Risks/LinkAction: R2. "target" is "story:{id}" or "evidence:{id}".
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> LinkAction(Guid id, Guid actionId, string? target, CancellationToken cancellationToken)
        {
            var (kind, value) = (target ?? "").Split(':') is [var k, var v] && Guid.TryParse(v, out var parsed)
                ? (k, parsed)
                : throw new DomainException("Choose a user story or evidence");

            await _service.LinkActionAsync(await ProjectOfAsync(id, cancellationToken), id, actionId,
                kind == "story" ? value : null, kind == "evidence" ? value : null, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Risks/CompleteAction: R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> CompleteAction(Guid id, Guid actionId, CancellationToken cancellationToken)
        {
            await _service.CompleteActionAsync(await ProjectOfAsync(id, cancellationToken), id, actionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<Guid> ProjectOfAsync(Guid riskId, CancellationToken cancellationToken)
            => (await _risks.GetAsync(riskId, cancellationToken))?.ProjectId ?? throw new DomainException("Risk not found");

        private static RiskInput Input(RiskFormModel model)
            => new(model.Title ?? "", model.Description, model.Likelihood, model.Impact, model.OwnerId, model.Treatment,
                model.ResidualLikelihood, model.ResidualImpact);
    }
}
