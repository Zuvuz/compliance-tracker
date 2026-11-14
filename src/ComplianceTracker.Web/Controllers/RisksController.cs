using ComplianceTracker.Web.Diagnostics;
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
        // US-119 R4: title and description fill in the form, e.g. from a gap on the Suggestions tab
        public async Task<IActionResult> Add(Guid projectId, CancellationToken cancellationToken, string? title = null, string? description = null)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            if (project is null || organization is null)
            {
                return NotFound();
            }

            var me = organization.Users.FirstOrDefault(u => u.ExternalId == User.FindFirst("sub")?.Value);
            return View(new RiskFormModel { ProjectId = project.Id, ProjectName = project.Name, OwnerId = me?.Id ?? Guid.Empty, Title = title, Description = description });
        }

        // POST /Risks/Add: R1–R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(RiskFormModel model, CancellationToken cancellationToken)
        {
            await _service.AddAsync(model.ProjectId, Input(model), cancellationToken);
            return RedirectToAction("Details", "Projects", new { id = model.ProjectId, tab = ProjectTabs.Risks });
        }

        // ---------------------------------------------------------- The AI agent's drafts (US-112)

        // POST /Risks/Draft: R1–R3, R6
        [AiRateLimit] // US-097 R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Details", Controller = "Projects", IdFrom = "projectId", Tab = ProjectTabs.Risks)]
        public async Task<IActionResult> Draft(Guid projectId, [FromServices] RiskDraftService drafts, CancellationToken cancellationToken)
        {
            var run = await drafts.DraftAsync(projectId, cancellationToken);
            TempData["RisksDrafted"] = $"The AI drafted {run.Made.Count} {(run.Made.Count == 1 ? "risk" : "risks")}"
                + (run.LeftOut.Count > 0 ? ". Left out: " + string.Join("; ", run.LeftOut.Select(l => $"\"{l.Draft}\" ({l.CoveredBy} covers it)")) : "")
                + (run.Silent.Count > 0 ? ". Nothing from " + string.Join("; ", run.Silent) : ""); // US-157 R4
            return RedirectToAction("Details", "Projects", new { id = projectId, tab = ProjectTabs.Risks });
        }

        // POST /Risks/AcceptDraft: R4, with the fields as the person left them
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Details", Controller = "Projects", IdFrom = "projectId", Tab = ProjectTabs.Risks)]
        public async Task<IActionResult> AcceptDraft(Guid projectId, Guid draftId, string? title, string? description, int likelihood, int impact,
            Guid ownerId, Domain.Risks.RiskTreatment treatment, [FromServices] RiskDraftService drafts, CancellationToken cancellationToken)
        {
            var risk = await drafts.AcceptAsync(projectId, draftId, new RiskInput(title ?? "", description, likelihood, impact, ownerId, treatment), cancellationToken);
            return RedirectToAction(nameof(Details), new { id = risk.Id });
        }

        // POST /Risks/DiscardDraft: R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Details", Controller = "Projects", IdFrom = "projectId", Tab = ProjectTabs.Risks)]
        public async Task<IActionResult> DiscardDraft(Guid projectId, Guid draftId, string? reason, [FromServices] RiskDraftService drafts, CancellationToken cancellationToken)
        {
            await drafts.DiscardAsync(projectId, draftId, reason, cancellationToken);
            return RedirectToAction("Details", "Projects", new { id = projectId, tab = ProjectTabs.Risks });
        }

        // ---------------------------------------------------------- Import (US-049)

        // GET /Risks/Import?projectId=...&importId=...: the upload, and the preview of an uploaded file (R3)
        [HttpGet]
        public async Task<IActionResult> Import(Guid projectId, Guid? importId, [FromServices] RiskImportService imports, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            if (project is null)
            {
                return NotFound();
            }

            if (!await imports.CanImportAsync(cancellationToken))
            {
                return Forbid(); // R8
            }

            RiskImportPreview? preview = null;
            if (importId is { } id)
            {
                try
                {
                    preview = await imports.PreviewAsync(projectId, id, cancellationToken);
                }
                catch (DomainException)
                {
                    return NotFound(); // another project's or organisation's import
                }
            }

            return View(new RiskImportViewModel(project, preview));
        }

        // POST /Risks/Import: R8, R9. Stores the file and shows its preview; nothing is saved as risks yet.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(2 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
        public async Task<IActionResult> Import(Guid projectId, IFormFile? file, [FromServices] RiskImportService imports, CancellationToken cancellationToken)
        {
            try
            {
                if (file is null || file.Length == 0)
                {
                    throw new DomainException("Choose a CSV file or an Excel workbook");
                }

                using var memory = new MemoryStream();
                await file.CopyToAsync(memory, cancellationToken);
                var import = await imports.UploadAsync(projectId, file.FileName, memory.ToArray(), cancellationToken);
                return RedirectToAction(nameof(Import), new { projectId, importId = import.Id });
            }
            catch (DomainException error)
            {
                TempData[DomainExceptionFilter.TempDataKey] = error.Message;
                return RedirectToAction(nameof(Import), new { projectId });
            }
        }

        // POST /Risks/ConfirmImport: R2, R4, R6, R7. All rows become risks, or none.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmImport(Guid projectId, Guid importId, bool importAnyway, [FromServices] RiskImportService imports, CancellationToken cancellationToken)
        {
            int count;
            try
            {
                count = await imports.ConfirmAsync(projectId, importId, importAnyway, cancellationToken);
            }
            catch (DomainException error)
            {
                // Back to the preview, with the reason
                TempData[DomainExceptionFilter.TempDataKey] = error.Message;
                return RedirectToAction(nameof(Import), new { projectId, importId });
            }

            TempData["RisksImported"] = $"Imported {count} {(count == 1 ? "risk" : "risks")}";
            return RedirectToAction("Details", "Projects", new { id = projectId, tab = ProjectTabs.Risks });
        }

        // GET /Risks/Template: R1, the columns to fill in, with one example row (";" as Danish Excel writes it)
        [HttpGet]
        public IActionResult Template()
        {
            var text = string.Join(";", RiskImportRows.Columns) + "\r\n"
                + "Stolen session cookie;Session cookie stolen via XSS;3;4;Anna;Reduce;;2026-05-01\r\n";
            return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(text)).ToArray(),
                "text/csv", "risk-register-template.csv");
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
        public async Task<IActionResult> Details(Guid id, [FromServices] EvidenceQueries evidence, [FromServices] Application.Ai.IRiskMeasureSuggestionRepository measures, CancellationToken cancellationToken)
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
                ProjectEvidence = (await evidence.ForProjectAsync(project.Id, cancellationToken))?.Evidence.Select(e => e.Evidence).ToList() ?? [],
                // US-106 R12
                MeasureSuggestions = (await measures.ListAsync(project.Id, cancellationToken)).Where(s => s.RiskId == risk.Id && s.IsPending)
                    .OrderByDescending(s => s.Score).ToList()
            });
        }

        // POST /Risks/SuggestMeasures: US-106 R12, the AI suggests requirements and stories that could reduce the risk
        [AiRateLimit] // US-097 R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> SuggestMeasures(Guid id, [FromServices] Application.Ai.AskTheAiService ai, CancellationToken cancellationToken)
        {
            var made = await ai.SuggestMeasuresAsync(await ProjectOfAsync(id, cancellationToken), id, cancellationToken);
            TempData["MeasureMessage"] = made == 0 ? "Nothing in the project matched well enough" : $"The AI suggests {made} {(made == 1 ? "measure" : "measures")}";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        // US-158: with optional evidence (a link or a file) that goes on the measure's requirement or story
        [RequestSizeLimit(MaxEvidenceRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxEvidenceRequestSize)]
        public async Task<IActionResult> ApproveMeasure(
            Guid id, Guid suggestionId, string? evidenceTitle, string? evidenceUrl, IFormFile? evidenceFile, DateOnly? evidenceValidUntil,
            [FromServices] MeasureEvidenceService measures, CancellationToken cancellationToken)
        {
            byte[]? content = null;
            if (evidenceFile is { Length: > 0 })
            {
                using var buffer = new MemoryStream();
                await evidenceFile.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }

            await measures.ApproveAsync(await ProjectOfAsync(id, cancellationToken), suggestionId,
                new MeasureEvidence(evidenceTitle, evidenceUrl, evidenceFile?.FileName, content, evidenceValidUntil), cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // Room for a 10 MB file and the form (EvidenceFile refuses anything larger with a clear message)
        private const long MaxEvidenceRequestSize = 25 * 1024 * 1024;

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> RejectMeasure(
            Guid id, Guid suggestionId, Domain.Mappings.RejectionReason? reason, string? comment, [FromServices] Application.Ai.AskTheAiService ai, CancellationToken cancellationToken)
        {
            await ai.RejectMeasureAsync(await ProjectOfAsync(id, cancellationToken), suggestionId, reason, comment, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Risks/Accept: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> Accept(Guid id, string? reason, CancellationToken cancellationToken)
        {
            if (!await _service.AcceptAsync(await ProjectOfAsync(id, cancellationToken), id, reason, cancellationToken))
            {
                // US-135 R2
                TempData["ApprovalRequested"] = "The risk is High or Critical, so another member must approve accepting it. It waits on the project's Overview.";
            }

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
