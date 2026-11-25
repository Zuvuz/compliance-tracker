using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-208: the ISMS area in the top bar. Its first page is the scope and context of the organisation (ISO/IEC 27001 4.1–4.4).
    // [Authorize] says it here as well as in the global policy, so CodeQL sees the check.
    [Authorize]
    public class IsmsController : Controller
    {
        // Rows for an interested party's requirements on the page
        public const int RequirementRows = 5;

        private readonly IsmsScopeService _scope;
        private readonly AssetService _assets;
        private readonly NonconformityService _nonconformities;
        private readonly ObjectiveService _objectives;
        private readonly AuditProgrammeService _programme;
        private readonly CompetenceService _competence;
        private readonly YearlyPlanService _plan;
        private readonly ManagementReviewService _review;
        private readonly CurrentMember _member;

        public IsmsController(IsmsScopeService scope, AssetService assets, NonconformityService nonconformities, ObjectiveService objectives,
            AuditProgrammeService programme, CompetenceService competence, YearlyPlanService plan, ManagementReviewService review, CurrentMember member)
        {
            _review = review;
            _plan = plan;
            _competence = competence;
            _programme = programme;
            _objectives = objectives;
            _scope = scope;
            _assets = assets;
            _nonconformities = nonconformities;
            _member = member;
        }

        // GET /Isms
        public IActionResult Index() => RedirectToAction(nameof(Scope));

        // GET /Isms/Scope: R6, everyone in the organisation reads it
        [HttpGet]
        public async Task<IActionResult> Scope(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await _scope.ViewAsync(cancellationToken));
        }

        // POST /Isms/Describe: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> Describe(string? text, string? locations, string? departments, string? internalIssues, string? externalIssues,
            Guid? ownerId, DateOnly? nextReviewOn, CancellationToken cancellationToken)
        {
            await _scope.DescribeAsync(new ScopeInput(text, locations, departments, internalIssues, externalIssues, ownerId, nextReviewOn), cancellationToken);
            TempData["Success"] = "The scope is saved.";
            return RedirectToAction(nameof(Scope));
        }

        // POST /Isms/Include: R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> Include(Guid projectId, CancellationToken cancellationToken)
        {
            await _scope.IncludeAsync(projectId, cancellationToken);
            return RedirectToAction(nameof(Scope), null, "scope-projects");
        }

        // POST /Isms/Exclude: R2, with the reason
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> Exclude(Guid projectId, string? reason, CancellationToken cancellationToken)
        {
            await _scope.ExcludeAsync(projectId, reason, cancellationToken);
            return RedirectToAction(nameof(Scope), null, "scope-projects");
        }

        // POST /Isms/Approve: 4.4, a new version
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> Approve(CancellationToken cancellationToken)
        {
            var version = await _scope.ApproveAsync(cancellationToken);
            TempData["Success"] = $"Version {version} of the scope is approved.";
            return RedirectToAction(nameof(Scope));
        }

        // GET /Isms/ScopePdf: R1
        [HttpGet]
        public async Task<IActionResult> ScopePdf(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return NotFound();
            }

            var file = await _scope.PdfAsync(cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }

        // POST /Isms/LockScope: R1, locked with a fingerprint on the organisation's Reports tab (US-050)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> LockScope(CancellationToken cancellationToken)
        {
            var snapshot = await _scope.LockAsync(cancellationToken);
            TempData["Success"] = $"The scope is locked. SHA-256 {snapshot.Sha256}";
            return RedirectToAction(nameof(Scope));
        }

        // POST /Isms/AddParty: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> AddParty(string? name, InterestedPartyKind? kind, List<string?>? requirement, List<int>? handled,
            CancellationToken cancellationToken)
        {
            var party = await _scope.AddPartyAsync(name, kind, Lines(requirement, handled), cancellationToken);
            return RedirectToAction(nameof(Scope), null, $"party-{party.Id}");
        }

        // POST /Isms/ChangeParty: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> ChangeParty(Guid partyId, string? name, InterestedPartyKind? kind, List<string?>? requirement, List<int>? handled,
            CancellationToken cancellationToken)
        {
            await _scope.ChangePartyAsync(partyId, name, kind, Lines(requirement, handled), cancellationToken);
            return RedirectToAction(nameof(Scope), null, $"party-{partyId}");
        }

        // POST /Isms/RemoveParty
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Scope))]
        public async Task<IActionResult> RemoveParty(Guid partyId, CancellationToken cancellationToken)
        {
            await _scope.RemovePartyAsync(partyId, cancellationToken);
            return RedirectToAction(nameof(Scope), null, "interested-parties");
        }

        // ---------------------------------------------------------- US-214: the inventory of information assets

        // GET /Isms/Assets: every member reads it, with filters
        [HttpGet]
        public async Task<IActionResult> Assets(AssetKind? kind, string? classification, bool personalData, bool noOwner, CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await _assets.ViewAsync(new AssetFilter(kind, classification, personalData, noOwner), cancellationToken));
        }

        // GET /Isms/AssetsExcel: the inventory as a workbook, with the page's filters
        [HttpGet]
        public async Task<IActionResult> AssetsExcel(AssetKind? kind, string? classification, bool personalData, bool noOwner, CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return NotFound();
            }

            var file = await _assets.ExcelAsync(new AssetFilter(kind, classification, personalData, noOwner), cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }

        // POST /Isms/AddAsset: R1, R2, R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Assets))]
        public async Task<IActionResult> AddAsset(string? name, AssetKind? kind, Guid? ownerId, string? classification, bool personalData, string? description,
            List<Guid>? projectIds, List<Guid>? supplierIds, List<Guid>? riskIds, CancellationToken cancellationToken)
        {
            var asset = await _assets.AddAsync(new AssetInput(name, kind, ownerId, classification, personalData, description, projectIds, supplierIds, riskIds),
                cancellationToken);
            TempData["Success"] = $"{asset.Name} is added.";
            return RedirectToAction(nameof(Assets), null, $"asset-{asset.Id}");
        }

        // POST /Isms/ChangeAsset
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Assets))]
        public async Task<IActionResult> ChangeAsset(Guid assetId, string? name, AssetKind? kind, Guid? ownerId, string? classification, bool personalData,
            string? description, List<Guid>? projectIds, List<Guid>? supplierIds, List<Guid>? riskIds, CancellationToken cancellationToken)
        {
            await _assets.ChangeAsync(assetId,
                new AssetInput(name, kind, ownerId, classification, personalData, description, projectIds, supplierIds, riskIds), cancellationToken);
            return RedirectToAction(nameof(Assets), null, $"asset-{assetId}");
        }

        // POST /Isms/RemoveAsset
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Assets))]
        public async Task<IActionResult> RemoveAsset(Guid assetId, CancellationToken cancellationToken)
        {
            await _assets.RemoveAsync(assetId, cancellationToken);
            return RedirectToAction(nameof(Assets));
        }

        // POST /Isms/ClassificationScheme: R2, one level per line, lowest first
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Assets))]
        public async Task<IActionResult> ClassificationScheme(string? levels, CancellationToken cancellationToken)
        {
            await _assets.ChangeSchemeAsync((levels ?? "").Split('\n'), cancellationToken);
            TempData["Success"] = "The classification scheme is saved.";
            return RedirectToAction(nameof(Assets), null, "classification-scheme");
        }

        // POST /Isms/ImportAssets: a CSV or Excel file; one bad row stops the import
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(2 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
        [OnDomainError(nameof(Assets))]
        public async Task<IActionResult> ImportAssets(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                throw new DomainException("Choose a CSV file or an Excel workbook");
            }

            using var memory = new MemoryStream();
            await file.CopyToAsync(memory, cancellationToken);
            var count = await _assets.ImportAsync(file.FileName, memory.ToArray(), cancellationToken);
            TempData["Success"] = $"{count} assets are imported.";
            return RedirectToAction(nameof(Assets));
        }

        // ---------------------------------------------------------- US-212: nonconformities and corrective actions

        // GET /Isms/Nonconformities: every member reads the register, with filters by status and source
        [HttpGet]
        public async Task<IActionResult> Nonconformities(NonconformityStatus? status, NonconformitySource? source, bool showClosed, CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await _nonconformities.ViewAsync(new NonconformityFilter(status, source, showClosed), cancellationToken));
        }

        // POST /Isms/RaiseNonconformity: R1, R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Nonconformities))]
        public async Task<IActionResult> RaiseNonconformity(string? title, string? description, NonconformitySource? source, NonconformitySeverity? severity,
            string? origin, CancellationToken cancellationToken)
        {
            var raised = await _nonconformities.RaiseAsync(title, description, source, severity, origin, cancellationToken);
            return RedirectToAction(nameof(Nonconformities), null, $"nonconformity-{raised.Id}");
        }

        // POST /Isms/AnalyseNonconformity: the correction and the cause
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Nonconformities))]
        public async Task<IActionResult> AnalyseNonconformity(Guid nonconformityId, string? correction, string? cause, CancellationToken cancellationToken)
        {
            await _nonconformities.AnalyseAsync(nonconformityId, correction, cause, cancellationToken);
            return RedirectToAction(nameof(Nonconformities), null, $"nonconformity-{nonconformityId}");
        }

        // POST /Isms/PlanCorrectiveAction
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Nonconformities))]
        public async Task<IActionResult> PlanCorrectiveAction(Guid nonconformityId, string? action, Guid? ownerId, DateOnly? dueOn, CancellationToken cancellationToken)
        {
            await _nonconformities.PlanActionAsync(nonconformityId, action, ownerId, dueOn, cancellationToken);
            return RedirectToAction(nameof(Nonconformities), null, $"nonconformity-{nonconformityId}");
        }

        // POST /Isms/CorrectiveActionDone
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Nonconformities))]
        public async Task<IActionResult> CorrectiveActionDone(Guid nonconformityId, CancellationToken cancellationToken)
        {
            await _nonconformities.MarkActionDoneAsync(nonconformityId, cancellationToken);
            return RedirectToAction(nameof(Nonconformities), null, $"nonconformity-{nonconformityId}");
        }

        // POST /Isms/CheckNonconformity: R3, R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Nonconformities))]
        public async Task<IActionResult> CheckNonconformity(Guid nonconformityId, string? result, bool effective, CancellationToken cancellationToken)
        {
            var closed = await _nonconformities.CheckAsync(nonconformityId, result, effective, cancellationToken);
            TempData["Success"] = closed ? "The action worked; the nonconformity is closed." : "The action did not work; plan a new corrective action.";
            return closed
                ? RedirectToAction(nameof(Nonconformities), null, new { showClosed = true }, $"nonconformity-{nonconformityId}")
                : RedirectToAction(nameof(Nonconformities), null, $"nonconformity-{nonconformityId}");
        }

        // POST /Isms/CloseNonconformity: R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Nonconformities))]
        public async Task<IActionResult> CloseNonconformity(Guid nonconformityId, CancellationToken cancellationToken)
        {
            await _nonconformities.CloseAsync(nonconformityId, cancellationToken);
            return RedirectToAction(nameof(Nonconformities), null, new { showClosed = true }, $"nonconformity-{nonconformityId}");
        }

        // POST /Isms/ReopenNonconformity
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Nonconformities))]
        public async Task<IActionResult> ReopenNonconformity(Guid nonconformityId, string? reason, CancellationToken cancellationToken)
        {
            await _nonconformities.ReopenAsync(nonconformityId, reason, cancellationToken);
            return RedirectToAction(nameof(Nonconformities), null, $"nonconformity-{nonconformityId}");
        }

        // ---------------------------------------------------------- US-209: information security objectives

        // GET /Isms/Objectives: every member reads them
        [HttpGet]
        public async Task<IActionResult> Objectives(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await _objectives.ViewAsync(cancellationToken));
        }

        // POST /Isms/AddObjective: R1. "measure" empty is a manual objective.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Objectives))]
        public async Task<IActionResult> AddObjective(string? text, Guid? ownerId, TrackerMeasure? measure, decimal? target, string? unit,
            ObjectiveDirection? direction, ObjectiveFrequency? frequency, DateOnly? dueOn, CancellationToken cancellationToken)
        {
            var objective = await _objectives.AddAsync(new ObjectiveInput(text, ownerId, measure, target, unit, direction, frequency, dueOn), cancellationToken);
            TempData["Success"] = objective.IsManual
                ? "The objective is added. Enter its first measurement."
                : "The objective is added. The tracker measures it within the hour.";
            return RedirectToAction(nameof(Objectives), null, $"objective-{objective.Id}");
        }

        // POST /Isms/ChangeObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Objectives))]
        public async Task<IActionResult> ChangeObjective(Guid objectiveId, string? text, Guid? ownerId, decimal? target, string? unit,
            ObjectiveDirection? direction, ObjectiveFrequency? frequency, DateOnly? dueOn, CancellationToken cancellationToken)
        {
            await _objectives.ChangeAsync(objectiveId, new ObjectiveInput(text, ownerId, null, target, unit, direction, frequency, dueOn), cancellationToken);
            return RedirectToAction(nameof(Objectives), null, $"objective-{objectiveId}");
        }

        // POST /Isms/RemoveObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Objectives))]
        public async Task<IActionResult> RemoveObjective(Guid objectiveId, CancellationToken cancellationToken)
        {
            await _objectives.RemoveAsync(objectiveId, cancellationToken);
            return RedirectToAction(nameof(Objectives));
        }

        // POST /Isms/RecordMeasurement: R3, R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Objectives))]
        public async Task<IActionResult> RecordMeasurement(Guid objectiveId, decimal? value, string? note, CancellationToken cancellationToken)
        {
            await _objectives.RecordAsync(objectiveId, value, note, cancellationToken);
            return RedirectToAction(nameof(Objectives), null, $"objective-{objectiveId}");
        }

        // ---------------------------------------------------------- US-211: the internal audit programme

        // GET /Isms/InternalAudits: every member reads the programme
        [HttpGet]
        public async Task<IActionResult> InternalAudits(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await _programme.ViewAsync(cancellationToken));
        }

        // POST /Isms/AuditCycle: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(InternalAudits))]
        public async Task<IActionResult> AuditCycle(DateOnly? startsOn, int? years, CancellationToken cancellationToken)
        {
            await _programme.SetCycleAsync(startsOn, years, cancellationToken);
            TempData["Success"] = "The audit programme is saved.";
            return RedirectToAction(nameof(InternalAudits));
        }

        // POST /Isms/ControlOwner: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(InternalAudits))]
        public async Task<IActionResult> ControlOwner(string? code, Guid? ownerId, CancellationToken cancellationToken)
        {
            await _programme.SetOwnerAsync(code, ownerId, cancellationToken);
            return RedirectToAction(nameof(InternalAudits), null, "control-owners");
        }

        // POST /Isms/PlanAudit: R2, R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(InternalAudits))]
        public async Task<IActionResult> PlanAudit(DateOnly? on, List<string?>? clauses, string? controls, List<Guid>? projectIds, Guid? auditorId,
            string? auditorName, string? reason, CancellationToken cancellationToken)
        {
            var planned = await _programme.PlanAsync(on, clauses, controls, projectIds, auditorId, auditorName, reason, cancellationToken);
            return RedirectToAction(nameof(InternalAudits), null, $"planned-{planned.Id}");
        }

        // POST /Isms/RemovePlannedAudit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(InternalAudits))]
        public async Task<IActionResult> RemovePlannedAudit(Guid plannedAuditId, CancellationToken cancellationToken)
        {
            await _programme.RemoveAsync(plannedAuditId, cancellationToken);
            return RedirectToAction(nameof(InternalAudits));
        }

        // POST /Isms/LinkRecordedAudit: R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(InternalAudits))]
        public async Task<IActionResult> LinkRecordedAudit(Guid plannedAuditId, Guid auditId, CancellationToken cancellationToken)
        {
            var raised = await _programme.LinkAsync(plannedAuditId, auditId, cancellationToken);
            TempData["Success"] = raised == 0 ? "The audit is done." : $"The audit is done; {raised} finding{(raised == 1 ? "" : "s")} went to the nonconformities.";
            return RedirectToAction(nameof(InternalAudits), null, $"planned-{plannedAuditId}");
        }

        // ---------------------------------------------------------- US-213: competence and awareness

        // GET /Isms/Competence: every member reads the matrix and confirms the policies they must read
        [HttpGet]
        public async Task<IActionResult> Competence(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await _competence.ViewAsync(cancellationToken));
        }

        // POST /Isms/AddCompetence: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Competence))]
        public async Task<IActionResult> AddCompetence(string? name, int? renewalMonths, List<Domain.Users.UserRole>? roles, bool forPeopleWithoutUser,
            CancellationToken cancellationToken)
        {
            await _competence.AddCompetenceAsync(name, renewalMonths, roles, forPeopleWithoutUser, cancellationToken);
            return RedirectToAction(nameof(Competence), null, "competence-matrix");
        }

        // POST /Isms/RemoveCompetence
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Competence))]
        public async Task<IActionResult> RemoveCompetence(Guid competenceId, CancellationToken cancellationToken)
        {
            await _competence.RemoveCompetenceAsync(competenceId, cancellationToken);
            return RedirectToAction(nameof(Competence), null, "competence-matrix");
        }

        // POST /Isms/AddStaffPerson: someone without a user
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Competence))]
        public async Task<IActionResult> AddStaffPerson(string? name, CancellationToken cancellationToken)
        {
            await _competence.AddPersonAsync(name, cancellationToken);
            return RedirectToAction(nameof(Competence), null, "competence-matrix");
        }

        // POST /Isms/RemoveStaffPerson: R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Competence))]
        public async Task<IActionResult> RemoveStaffPerson(Guid staffPersonId, CancellationToken cancellationToken)
        {
            await _competence.RemovePersonAsync(staffPersonId, cancellationToken);
            return RedirectToAction(nameof(Competence), null, "competence-matrix");
        }

        // POST /Isms/RecordTraining: R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Competence))]
        public async Task<IActionResult> RecordTraining(Guid competenceId, string? person, DateOnly? trainedOn, string? evidence, CancellationToken cancellationToken)
        {
            await _competence.RecordTrainingAsync(competenceId, person, trainedOn, evidence, cancellationToken);
            TempData["Success"] = "The training is recorded.";
            return RedirectToAction(nameof(Competence), null, "competence-matrix");
        }

        // POST /Isms/ConfirmPolicy: R3, R4, the member confirms themself
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Competence))]
        public async Task<IActionResult> ConfirmPolicy(Guid documentId, CancellationToken cancellationToken)
        {
            await _competence.ConfirmPolicyAsync(documentId, cancellationToken);
            return RedirectToAction(nameof(Competence), null, "policies-to-read");
        }

        // ---------------------------------------------------------- US-215: the yearly plan

        // GET /Isms/YearlyPlan: every member reads the plan
        [HttpGet]
        public async Task<IActionResult> YearlyPlan(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await _plan.ViewAsync(cancellationToken));
        }

        // POST /Isms/AddRecurringTask: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(YearlyPlan))]
        public async Task<IActionResult> AddRecurringTask(string? title, string? description, TaskInterval? interval, Guid? ownerId, DateOnly? nextDueOn,
            string? controls, string? evidenceNeeded, CancellationToken cancellationToken)
        {
            var task = await _plan.AddAsync(new RecurringTaskInput(title, description, interval, ownerId, nextDueOn, controls, evidenceNeeded), cancellationToken);
            return RedirectToAction(nameof(YearlyPlan), null, $"task-{task.Id}");
        }

        // POST /Isms/AcceptTaskSuggestion: R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(YearlyPlan))]
        public async Task<IActionResult> AcceptTaskSuggestion(string? key, Guid? ownerId, DateOnly? nextDueOn, CancellationToken cancellationToken)
        {
            var task = await _plan.AcceptSuggestionAsync(key, ownerId, nextDueOn, cancellationToken);
            return RedirectToAction(nameof(YearlyPlan), null, $"task-{task.Id}");
        }

        // POST /Isms/RemoveRecurringTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(YearlyPlan))]
        public async Task<IActionResult> RemoveRecurringTask(Guid taskId, CancellationToken cancellationToken)
        {
            await _plan.RemoveAsync(taskId, cancellationToken);
            return RedirectToAction(nameof(YearlyPlan));
        }

        // POST /Isms/CompleteRecurringTask: R2, R3, R4, with the evidence file
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
        [OnDomainError(nameof(YearlyPlan))]
        public async Task<IActionResult> CompleteRecurringTask(Guid taskId, DateOnly? doneOn, string? note, IFormFile? file, CancellationToken cancellationToken)
        {
            byte[]? content = null;
            if (file is { Length: > 0 })
            {
                using var memory = new MemoryStream();
                await file.CopyToAsync(memory, cancellationToken);
                content = memory.ToArray();
            }

            await _plan.CompleteAsync(taskId, doneOn, note, file?.FileName, content, cancellationToken);
            TempData["Success"] = "The task is done.";
            return RedirectToAction(nameof(YearlyPlan), null, $"task-{taskId}");
        }

        // POST /Isms/RecurringTaskNonconformity: R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(YearlyPlan))]
        public async Task<IActionResult> RecurringTaskNonconformity(Guid taskId, CancellationToken cancellationToken)
        {
            var nonconformity = await _plan.RaiseNonconformityAsync(taskId, cancellationToken);
            return RedirectToAction(nameof(Nonconformities), null, $"nonconformity-{nonconformity.Id}");
        }

        // ---------------------------------------------------------- US-210: the management review

        // GET /Isms/ManagementReview: every member reads the reviews; reviewId shows an earlier one
        [HttpGet]
        public async Task<IActionResult> ManagementReview(Guid? reviewId, CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            var view = await _review.ViewAsync(reviewId, cancellationToken);
            return reviewId is { } id && view.Current?.Id != id ? NotFound() : View(view);
        }

        // POST /Isms/PlanReview: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(ManagementReview))]
        public async Task<IActionResult> PlanReview(DateOnly? plannedOn, List<string?>? participants, string? otherParticipants, CancellationToken cancellationToken)
        {
            var review = await _review.PlanAsync(plannedOn, Participants(participants, otherParticipants), cancellationToken);
            return RedirectToAction(nameof(ManagementReview), new { reviewId = review.Id });
        }

        // POST /Isms/ChangeReview: the date, participants and the manual inputs e) and g)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(ManagementReview))]
        public async Task<IActionResult> ChangeReview(Guid reviewId, DateOnly? plannedOn, List<string?>? participants, string? otherParticipants,
            string? feedback, string? improvements, CancellationToken cancellationToken)
        {
            await _review.ChangeAsync(reviewId, plannedOn, Participants(participants, otherParticipants), feedback, improvements, cancellationToken);
            return RedirectToAction(nameof(ManagementReview), new { reviewId });
        }

        // POST /Isms/HoldReview: R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(ManagementReview))]
        public async Task<IActionResult> HoldReview(Guid reviewId, DateOnly? heldOn, CancellationToken cancellationToken)
        {
            await _review.HoldAsync(reviewId, heldOn, cancellationToken);
            return RedirectToAction(nameof(ManagementReview), new { reviewId });
        }

        // POST /Isms/ReviewDecision: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(ManagementReview))]
        public async Task<IActionResult> ReviewDecision(Guid reviewId, string? text, Guid? actionOwnerId, DateOnly? actionDueOn, CancellationToken cancellationToken)
        {
            await _review.DecideAsync(reviewId, text, actionOwnerId, actionDueOn, cancellationToken);
            return RedirectToAction(nameof(ManagementReview), null, new { reviewId }, "review-decisions");
        }

        // POST /Isms/LockReview: R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(ManagementReview))]
        public async Task<IActionResult> LockReview(Guid reviewId, CancellationToken cancellationToken)
        {
            await _review.LockAsync(reviewId, cancellationToken);
            TempData["Success"] = "The minutes are locked, and the PDF is on the organisation's Reports tab.";
            return RedirectToAction(nameof(ManagementReview), new { reviewId });
        }

        // POST /Isms/ReviewActionDone: R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(ManagementReview))]
        public async Task<IActionResult> ReviewActionDone(Guid reviewId, Guid decisionId, CancellationToken cancellationToken)
        {
            await _review.MarkActionDoneAsync(reviewId, decisionId, cancellationToken);
            return RedirectToAction(nameof(ManagementReview), null, new { reviewId }, "review-decisions");
        }

        // GET /Isms/ReviewPdf
        [HttpGet]
        public async Task<IActionResult> ReviewPdf(Guid reviewId, CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return NotFound();
            }

            if ((await _review.ViewAsync(reviewId, cancellationToken)).Current?.Id != reviewId)
            {
                return NotFound();
            }

            var file = await _review.PdfAsync(reviewId, cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }

        // The participants ticked among the members and the others written one per line
        public static IReadOnlyList<string?> Participants(List<string?>? members, string? others)
            => (members ?? []).Concat((others ?? "").Split('\n')).ToList();

        // The requirement rows of the form: the texts, and the numbers of the rows ticked as handled
        public static IReadOnlyList<(string? Text, bool Handled)> Lines(List<string?>? requirement, List<int>? handled)
            => (requirement ?? []).Select((text, i) => (text, (handled ?? []).Contains(i))).ToList();
    }
}
