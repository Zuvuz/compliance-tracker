using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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
        private readonly TimeProvider _clock;
        private readonly ComplianceService _compliance;
        private readonly ComplianceQueries _complianceQueries;
        private readonly MappingSuggestionService _suggestions;
        private readonly RiskQueries _risks;
        private readonly RequirementDocumentService _documents;
        private readonly GetStartedService _getStarted;

        public ProjectsController(
            ProjectService service,
            ProjectQueries projects,
            FrameworkQueries frameworks,
            OrganizationQueries organizations,
            WorkItemSyncService sync,
            WorkItemQueries workItems,
            ComplianceService compliance,
            ComplianceQueries complianceQueries,
            MappingSuggestionService suggestions,
            RiskQueries risks,
            RequirementDocumentService documents,
            GetStartedService getStarted,
            TimeProvider clock)
        {
            _getStarted = getStarted;
            _documents = documents;
            _risks = risks;
            _suggestions = suggestions;
            _compliance = compliance;
            _complianceQueries = complianceQueries;
            _clock = clock;
            _service = service;
            _projects = projects;
            _frameworks = frameworks;
            _organizations = organizations;
            _sync = sync;
            _workItems = workItems;
        }

        // GET /Projects: the front page after sign-in (US-045 R4). The organisation's projects (US-008 R4)
        // and which framework versions they use (US-011 R5).
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(new OrganizationOverviewViewModel(
                organization,
                await _projects.ListAsync(cancellationToken),
                await _frameworks.ListAsync(cancellationToken),
                _clock.Today())
            {
                GetStarted = await _getStarted.ForMeAsync(cancellationToken) // US-096
            });
        }

        // POST /Projects/HideGetStarted and /Projects/ShowGetStarted: US-096 R4, for the signed-in member only
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HideGetStarted(CancellationToken cancellationToken)
        {
            await _getStarted.SetHiddenAsync(true, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShowGetStarted(CancellationToken cancellationToken)
        {
            await _getStarted.SetHiddenAsync(false, cancellationToken);
            return RedirectToAction(nameof(Index));
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

        // GET /Projects/Details/{id}/{tab?}: the project's own page (US-008), split into tabs (US-022).
        // Each tab has its own address (R6), e.g. /Projects/Details/{id}/frameworks. Without a tab: Overview.
        // US-041: the Compliance tab has one sub-tab per framework, e.g. /Projects/Details/{id}/compliance/cra.
        // q searches the requirements (US-019 R3); show filters them (US-021 R6, US-044 R2: ComplianceFilters).
        [HttpGet("Projects/Details/{id:guid}/{tab?}/{sub?}")]
        // US-042: on the Risks tab, status, level and due=1 filter the register (R8)
        public async Task<IActionResult> Details(
            Guid id, string? tab, string? sub, string? q, string? show,
            string? status, string? level, string? due,
            // US-101: the Activity tab's filters (q is the search there too)
            string? from, string? to, string? group, string? user, string? sort, string? page,
            [FromServices] ClassificationService classifications,
            [FromServices] Application.Security.SecurityEventQueries securityEventQueries,
            [FromServices] EvidenceQueries evidenceQueries,
            [FromServices] Application.Scanning.ScanService scans,
            [FromServices] Application.Security.SecurityEventSuggestionService suggestionService,
            CancellationToken cancellationToken)
        {
            // US-104 R1: shared links to the old "User stories" tab still work
            if (string.Equals(tab, ProjectTabs.OldProductRequirements, StringComparison.OrdinalIgnoreCase))
            {
                // RedirectToAction keeps the redirect inside the app (US-094 R4); the filters in the address go along
                var values = new RouteValueDictionary(Request.Query.ToDictionary(q => q.Key, q => (object?)q.Value.ToString()))
                {
                    ["id"] = id,
                    ["tab"] = ProjectTabs.ProductRequirements,
                    ["sub"] = sub
                };
                return RedirectToActionPermanent(nameof(Details), values);
            }

            var activeTab = ProjectTabs.Find(tab);
            var project = await _projects.GetAsync(id, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            if (project is null || organization is null || activeTab is null)
            {
                return NotFound(); // US-008 R5: also when the project belongs to another organisation
            }

            // The Overview shows the latest entries (US-048 R5); the Activity tab searches the log (US-101)
            var activity = activeTab is ProjectTabs.Overview
                ? await _projects.ActivityAsync(id, cancellationToken)
                : [];
            var (activityQuery, activityIgnored) = ActivityFilters.Parse(q, from, to, group, user, sort, page);
            var activityPage = activeTab is ProjectTabs.Activity
                ? await _projects.SearchActivityAsync(id, activityQuery, cancellationToken)
                : null;

            // Requirements and links: for the Compliance tab, the overview's summary and the user stories' requirements.
            // US-129: the Reports tab, for which reports can be made.
            var compliance = activeTab is ProjectTabs.Compliance or ProjectTabs.Overview or ProjectTabs.ProductRequirements or ProjectTabs.Scans
                or ProjectTabs.Reports
                ? await _complianceQueries.ForProjectAsync(id, cancellationToken)
                : null;

            // US-059: the Evidence tab, and the evidence that can be picked on the Compliance tab
            var evidenceTab = activeTab is ProjectTabs.Evidence or ProjectTabs.Compliance
                ? await evidenceQueries.ForProjectAsync(id, cancellationToken)
                : null;

            // US-042: the register, sorted by level; closed risks only when asked for (R7). US-048 R3: the overview counts them.
            var allRisks = activeTab is ProjectTabs.Risks or ProjectTabs.Overview ? await _risks.ForProjectAsync(id, cancellationToken) ?? [] : [];
            var classified = activeTab is ProjectTabs.Overview ? await classifications.ForProjectAsync(id, cancellationToken) ?? [] : [];
            var securityEvents = activeTab is ProjectTabs.SecurityEvents or ProjectTabs.Overview
                ? await securityEventQueries.ForProjectAsync(id, cancellationToken) ?? []
                : [];
            var scanOverview = activeTab is ProjectTabs.Scans or ProjectTabs.Overview ? await scans.ForProjectAsync(id, cancellationToken) : null;
            // US-128: suggested security events, on their tab and under "Needs attention"
            var eventSuggestions = activeTab is ProjectTabs.SecurityEvents or ProjectTabs.Overview
                ? await suggestionService.ForProjectAsync(id, cancellationToken)
                : null;
            var now = _clock.GetUtcNow().UtcDateTime;
            var risks = allRisks
                .Where(r => string.IsNullOrEmpty(status) ? r.Status != RiskStatus.Closed : r.DisplayStatus.Equals(status, StringComparison.OrdinalIgnoreCase))
                .Where(r => string.IsNullOrEmpty(level) || r.Level.ToString().Equals(level, StringComparison.OrdinalIgnoreCase))
                .Where(r => due != "1" || r.IsReviewDue(now))
                .ToList();

            // An unknown sub-tab is "not found", like an unknown tab
            if (activeTab == ProjectTabs.Compliance && sub is not null
                && compliance?.Frameworks.Any(f => FrameworkNames.Slug(f.Framework.Name) == sub) != true)
            {
                return NotFound();
            }

            return View(new ProjectDetailsViewModel(
                project,
                await _frameworks.ListAsync(cancellationToken),
                await _workItems.ListAsync(id, cancellationToken) ?? [],
                _clock.Today())
            {
                // US-032: the requirement specifications, for the User stories tab
                Documents = activeTab == ProjectTabs.ProductRequirements ? await _workItems.DocumentsAsync(id, cancellationToken) : [],
                // US-107 R3: the organisation's GitHub Projects, while one is still to be chosen
                GitHubProjectChoices = activeTab == ProjectTabs.ProductRequirements && project.GitHub is { NeedsProjectChoice: true }
                    ? await _sync.ProjectsToChooseAsync(id, cancellationToken)
                    : [],
                Tab = activeTab,
                Organization = organization,
                Activity = activity,
                ActivityPage = activityPage,
                ActivityQuery = activityQuery,
                ActivityIgnored = activityIgnored,
                Compliance = compliance,
                EvidenceTab = evidenceTab,
                Sub = sub,
                Search = q,
                ComplianceFilter = ComplianceFilters.Find(show),
                Risks = risks,
                AllRisks = allRisks,
                Classifications = classified,
                SecurityEvents = securityEvents,
                EventSuggestions = eventSuggestions,
                Scans = scanOverview,
                RiskStatusFilter = status,
                RiskLevelFilter = level,
                OnlyReviewDue = due == "1",
                Now = now
            });
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

            return PartialView("_WorkItems", new WorkItemsViewModel(
                project, workItems, await _complianceQueries.ForProjectAsync(id, cancellationToken), await _workItems.DocumentsAsync(id, cancellationToken)));
        }

        // ---------------------------------------------------------- Requirement specifications (US-032)

        // Room for a 10 MB file and the form (RequirementFile refuses anything larger with a clear message)
        private const long MaxDocumentRequestSize = 25 * 1024 * 1024;

        // POST /Projects/UploadRequirementDocument: R1-R10. documentId: the specification this is a new version of;
        // empty means "the one with the same file name, or a new one".
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxDocumentRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxDocumentRequestSize)]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        // US-033: idPattern says how IDs look in a Word document. US-034: kind is requirements or use cases.
        public async Task<IActionResult> UploadRequirementDocument(
            Guid projectId, IFormFile? file, Guid? documentId, string? idPattern, Domain.WorkItems.DocumentKind? kind,
            CancellationToken cancellationToken)
        {
            if (file is not { Length: > 0 })
            {
                throw new Domain.Common.DomainException("Choose a file to upload");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            await _documents.UploadAsync(projectId, file.FileName, buffer.ToArray(), documentId, idPattern,
                kind ?? Domain.WorkItems.DocumentKind.RequirementSpecification, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // GET /Projects/RequirementDocumentFile/{id}?versionId=...: R7, for anyone in the organisation (R8: 404 for others)
        [HttpGet]
        public async Task<IActionResult> RequirementDocumentFile(Guid id, Guid versionId, CancellationToken cancellationToken)
        {
            var file = await _workItems.DocumentFileAsync(id, versionId, cancellationToken);
            if (file is null)
            {
                return NotFound();
            }

            // ASVS V5.4.1: always a download with the stored content type and our cleaned name; nosniff stops guessing
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(file.Value.Content, file.Value.Version.ContentType, file.Value.Version.FileName);
        }

        // POST /Projects/LinkGitHub: R1. US-107: the address may also be an organisation's GitHub Project or the organisation,
        // and a token can be given at once (needed for a GitHub Project). The token is never sent back to the page.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> LinkGitHub(Guid projectId, string? repository, string? label, string? token, CancellationToken cancellationToken)
        {
            await _sync.LinkGitHubAddressAsync(projectId, repository ?? "", label, token, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // POST /Projects/ChooseGitHubProject: US-107 R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> ChooseGitHubProject(Guid projectId, int number, CancellationToken cancellationToken)
        {
            await _sync.ChooseGitHubProjectAsync(projectId, number, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // POST /Projects/SetGitHubToken: US-031 R1-R3. A refused token is shown as an error and never sent back to the page.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> SetGitHubToken(Guid projectId, string? token, CancellationToken cancellationToken)
        {
            await _sync.SetGitHubTokenAsync(projectId, token ?? "", cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // POST /Projects/RemoveGitHubToken: US-031 R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> RemoveGitHubToken(Guid projectId, CancellationToken cancellationToken)
        {
            await _sync.RemoveGitHubTokenAsync(projectId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // POST /Projects/SyncNow: R5. A failed sync is shown on the page from the project's GitHub link.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> SyncNow(Guid projectId, CancellationToken cancellationToken)
        {
            await _sync.SyncNowAsync(projectId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // ---------------------------------------------------------- Requirements and user stories (US-021)

        // POST /Projects/LinkRequirement: links one of the project's user stories to a requirement, by hand
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> LinkRequirement(Guid projectId, Guid requirementId, Guid workItemId, string? sub, CancellationToken cancellationToken)
        {
            await _compliance.LinkAsync(projectId, requirementId, workItemId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/UnlinkRequirement: the link is kept as history
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> UnlinkRequirement(Guid projectId, Guid linkId, string? sub, CancellationToken cancellationToken)
        {
            await _compliance.UnlinkAsync(projectId, linkId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/MarkNotApplicable: US-043 R5. The reason is required.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> MarkNotApplicable(Guid projectId, Guid requirementId, string? reason, string? sub, CancellationToken cancellationToken)
        {
            await _compliance.MarkNotApplicableAsync(projectId, requirementId, reason ?? "", cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/MarkApplicable: US-043 R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> MarkApplicable(Guid projectId, Guid requirementId, string? sub, CancellationToken cancellationToken)
        {
            await _compliance.MarkApplicableAsync(projectId, requirementId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/ConfirmReview: US-110 R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> ConfirmReview(Guid projectId, Guid requirementId, string? sub, string? show, CancellationToken cancellationToken)
        {
            await _compliance.ConfirmReviewAsync(projectId, requirementId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub, show });
        }

        // ---------------------------------------------------------- Evidence (US-057)

        // Room for a 10 MB file and the form (EvidenceFile refuses anything larger with a clear message)
        private const long MaxEvidenceRequestSize = 25 * 1024 * 1024;

        // POST /Projects/AddEvidence: R1, R2. A link, or a file if one is chosen.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxEvidenceRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxEvidenceRequestSize)]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> AddEvidence(
            Guid projectId, Guid requirementId, string? title, string? url, IFormFile? file, DateOnly? validUntil, string? sub,
            CancellationToken cancellationToken)
        {
            if (file is { Length: > 0 })
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    throw new Domain.Common.DomainException("Give a link or a file, not both");
                }

                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                await _compliance.AddEvidenceFileAsync(projectId, requirementId, title ?? "", file.FileName, buffer.ToArray(), validUntil, cancellationToken);
            }
            else
            {
                await _compliance.AddEvidenceLinkAsync(projectId, requirementId, title ?? "", url ?? "", validUntil, cancellationToken);
            }

            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/RemoveEvidence: R5, kept as history. US-059: off this requirement; removed when it proved nothing else.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> RemoveEvidence(Guid projectId, Guid evidenceId, Guid requirementId, string? sub, CancellationToken cancellationToken)
        {
            await _compliance.RemoveEvidenceAsync(projectId, evidenceId, requirementId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/LinkExistingEvidence: US-059, "Pick existing evidence" on a Compliance tab row
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> LinkExistingEvidence(Guid projectId, Guid requirementId, Guid evidenceId, string? sub, CancellationToken cancellationToken)
        {
            await _compliance.LinkExistingEvidenceAsync(projectId, requirementId, evidenceId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // ---------------------------------------------------------- Evidence suggestions (US-058)

        // POST /Projects/UploadTestResults: R1, R2, R9
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxEvidenceRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxEvidenceRequestSize)]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> UploadTestResults(
            Guid projectId, IFormFile? file, [FromServices] EvidenceSuggestionService suggestions, CancellationToken cancellationToken)
        {
            if (file is not { Length: > 0 })
            {
                throw new Domain.Common.DomainException("Choose a test results file");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            var result = await suggestions.UploadTestResultsAsync(projectId, file.FileName, buffer.ToArray(), cancellationToken);
            TempData["TestResults"] = $"{result.Suggested} suggested, {result.Blocked} blocked by failing tests"
                + (result.UnknownTags.Count > 0 ? $"; unknown tags ignored: {string.Join(", ", result.UnknownTags)}" : "");
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // POST /Projects/AcceptSuggestedEvidence: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> AcceptSuggestedEvidence(
            Guid projectId, Guid suggestionId, string? sub, [FromServices] EvidenceSuggestionService suggestions, CancellationToken cancellationToken)
        {
            await suggestions.AcceptAsync(projectId, suggestionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/RejectSuggestedEvidence: R5, with a reason
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> RejectSuggestedEvidence(
            Guid projectId, Guid suggestionId, string? reason, string? sub, [FromServices] EvidenceSuggestionService suggestions, CancellationToken cancellationToken)
        {
            await suggestions.RejectAsync(projectId, suggestionId, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // ---------------------------------------------------------- Evidence tab (US-059)

        // POST /Projects/UploadEvidence: R1, then the AI suggests requirements (R3)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxEvidenceRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxEvidenceRequestSize)]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> UploadEvidence(
            Guid projectId, string? title, string? url, IFormFile? file, DateOnly? validUntil,
            [FromServices] EvidenceService evidence, CancellationToken cancellationToken)
        {
            byte[]? content = null;
            if (file is { Length: > 0 })
            {
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }

            var added = await evidence.AddAsync(projectId, title ?? "", url, file?.FileName, content, validUntil, cancellationToken);
            if (added.AiUnavailable is not null)
            {
                TempData["EvidenceAiUnavailable"] = $"No suggestions: {added.AiUnavailable}";
            }
            else if (added.Suggestions == 0)
            {
                // US-105 R3: nothing reached the limit, so nothing weak is shown
                TempData["EvidenceAiUnavailable"] = "No requirement looks like a match; link it by hand.";
            }

            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // POST /Projects/LinkEvidence: R2, one evidence to one or more requirements
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> LinkEvidence(
            Guid projectId, Guid evidenceId, List<Guid> requirementIds, [FromServices] EvidenceService evidence, CancellationToken cancellationToken)
        {
            await evidence.LinkAsync(projectId, evidenceId, requirementIds, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // POST /Projects/UnlinkEvidence: R7, kept as history
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> UnlinkEvidence(Guid projectId, Guid linkId, [FromServices] EvidenceService evidence, CancellationToken cancellationToken)
        {
            await evidence.UnlinkAsync(projectId, linkId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // POST /Projects/RemoveProjectEvidence: R7, off every requirement
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> RemoveProjectEvidence(Guid projectId, Guid evidenceId, [FromServices] EvidenceService evidence, CancellationToken cancellationToken)
        {
            await evidence.RemoveAsync(projectId, evidenceId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // POST /Projects/MoreEvidenceSuggestions: R3, "Show more"
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> MoreEvidenceSuggestions(Guid projectId, Guid evidenceId, [FromServices] EvidenceService evidence, CancellationToken cancellationToken)
        {
            try
            {
                await evidence.ShowMoreAsync(projectId, evidenceId, cancellationToken);
            }
            catch (Application.Ai.AiUnavailableException e)
            {
                TempData["EvidenceAiUnavailable"] = $"No suggestions: {e.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // POST /Projects/ApproveEvidenceSuggestion: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> ApproveEvidenceSuggestion(Guid projectId, Guid suggestionId, [FromServices] EvidenceService evidence, CancellationToken cancellationToken)
        {
            await evidence.ApproveAsync(projectId, suggestionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // POST /Projects/RejectEvidenceSuggestion: R4, with a reason
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Evidence)]
        public async Task<IActionResult> RejectEvidenceSuggestion(
            Guid projectId, Guid suggestionId, Domain.Mappings.RejectionReason? reason, string? comment,
            [FromServices] EvidenceService evidence, CancellationToken cancellationToken)
        {
            await evidence.RejectAsync(projectId, suggestionId, reason, comment, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Evidence });
        }

        // GET /Projects/EvidenceFile/{id}?evidenceId=...: R6, for anyone in the organisation
        [HttpGet]
        public async Task<IActionResult> EvidenceFile(Guid id, Guid evidenceId, CancellationToken cancellationToken)
        {
            var file = await _complianceQueries.EvidenceFileAsync(id, evidenceId, cancellationToken);
            if (file is null)
            {
                return NotFound(); // also for another organisation's evidence
            }

            // ASVS V5.4.1: always a download with the stored content type and our cleaned name; nosniff stops guessing
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(file.Value.Content, file.Value.Evidence.ContentType!, file.Value.Evidence.FileName);
        }

        // ---------------------------------------------------------- Classification (US-026)

        // POST /Projects/Classify: R1–R4, R7
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> Classify(
            Guid projectId, Guid frameworkId, string? classCode, string? reason,
            [FromServices] ClassificationService classifications, CancellationToken cancellationToken)
        {
            await classifications.ClassifyAsync(projectId, frameworkId, classCode, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // ---------------------------------------------------------- Security events (US-073)

        // The forms send local times (datetime-local); the domain keeps UTC
        private static DateTime Utc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

        // POST /Projects/RecordSecurityEvent: R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> RecordSecurityEvent(
            Guid projectId, Domain.Security.SecurityEventKind kind, string? title, string? description, DateTime? awareAt,
            [FromServices] Application.Security.SecurityEventService events, CancellationToken cancellationToken)
        {
            if (awareAt is not { } aware)
            {
                throw new Domain.Common.DomainException("Give the time you became aware of it");
            }

            await events.RecordAsync(projectId, kind, title ?? "", description, Utc(aware), cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // ---------------------------------------------------------- Scans (US-078)

        // Room for a 50 MB SARIF file and the form (Scan refuses anything larger with a clear message)
        private const long MaxScanRequestSize = 55 * 1024 * 1024;

        // POST /Projects/UploadScan: R1–R3, R7
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxScanRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxScanRequestSize)]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Scans)]
        public async Task<IActionResult> UploadScan(
            Guid projectId, IFormFile? file, [FromServices] Application.Scanning.ScanService scans, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                throw new Domain.Common.DomainException("Choose a SARIF file");
            }

            using var memory = new MemoryStream();
            await file.CopyToAsync(memory, cancellationToken);
            var scan = await scans.UploadAsync(projectId, file.FileName, memory.ToArray(), cancellationToken);
            TempData["ScanImported"] = scan.Summary;
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Scans });
        }

        // POST /Projects/CreateUploadToken: US-122 R2, the token is shown once on the next page
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Scans)]
        public async Task<IActionResult> CreateUploadToken(
            Guid projectId, string? name, [FromServices] Application.Scanning.ScanService scans, CancellationToken cancellationToken)
        {
            TempData["NewUploadToken"] = await scans.CreateUploadTokenAsync(projectId, name, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Scans });
        }

        // POST /Projects/RevokeUploadToken: US-122 R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Scans)]
        public async Task<IActionResult> RevokeUploadToken(
            Guid projectId, Guid tokenId, [FromServices] Application.Scanning.ScanService scans, CancellationToken cancellationToken)
        {
            await scans.RevokeUploadTokenAsync(projectId, tokenId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Scans });
        }

        // POST /Projects/FetchScansFromGitHub: US-121 R1, R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Scans)]
        public async Task<IActionResult> FetchScansFromGitHub(
            Guid projectId, [FromServices] Application.Scanning.ScanService scans, CancellationToken cancellationToken)
        {
            var lines = await scans.FetchFromGitHubAsync(projectId, cancellationToken);
            TempData["ScanImported"] = string.Join("\n", lines);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Scans });
        }

        // POST /Projects/AcceptFinding: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Scans)]
        public async Task<IActionResult> AcceptFinding(
            Guid projectId, Guid findingId, string? reason, [FromServices] Application.Scanning.ScanService scans, CancellationToken cancellationToken)
        {
            await scans.AcceptFindingAsync(projectId, findingId, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Scans });
        }

        // POST /Projects/ScanAsEvidence: R5. The scan file becomes evidence for the requirement (US-057), with its SHA-256.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Scans)]
        public async Task<IActionResult> ScanAsEvidence(
            Guid projectId, Guid scanId, Guid requirementId, [FromServices] Application.Scanning.ScanService scans, CancellationToken cancellationToken)
        {
            var (scan, content) = await scans.FileAsync(projectId, scanId, cancellationToken)
                ?? throw new Domain.Common.DomainException("Scan not found");
            var fileName = Path.ChangeExtension(scan.FileName, ".sarif");
            await _compliance.AddEvidenceFileAsync(projectId, requirementId, $"{scan.Tool} scan {scan.ScannedAt.ToLocalTime():dd-MM-yyyy}",
                fileName, content, null, cancellationToken);
            TempData["ScanImported"] = $"{scan.Tool} scan added as evidence";
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Scans });
        }

        // POST /Projects/MarkSecurityReportSent: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> MarkSecurityReportSent(
            Guid projectId, Guid eventId, Domain.Security.SecurityReportKind report, DateTime? sentAt, string? reference,
            [FromServices] Application.Security.SecurityEventService events, CancellationToken cancellationToken)
        {
            var at = sentAt is { } sent ? Utc(sent) : _clock.GetUtcNow().UtcDateTime;
            await events.MarkSentAsync(projectId, eventId, report, at, reference, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // POST /Projects/MarkSecurityFixAvailable: Art. 14(2)(c)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> MarkSecurityFixAvailable(
            Guid projectId, Guid eventId, DateTime? fixAvailableAt,
            [FromServices] Application.Security.SecurityEventService events, CancellationToken cancellationToken)
        {
            var at = fixAvailableAt is { } fix ? Utc(fix) : _clock.GetUtcNow().UtcDateTime;
            await events.MarkFixAvailableAsync(projectId, eventId, at, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // POST /Projects/CloseSecurityEvent: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> CloseSecurityEvent(
            Guid projectId, Guid eventId, string? reason, [FromServices] Application.Security.SecurityEventService events, CancellationToken cancellationToken)
        {
            await events.CloseAsync(projectId, eventId, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // POST /Projects/RecordSuggestedSecurityEvent: US-128 R1, the awareness time can be changed
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> RecordSuggestedSecurityEvent(
            Guid projectId, Guid suggestionId, DateTime? awareAt, [FromServices] Application.Security.SecurityEventSuggestionService suggestions,
            CancellationToken cancellationToken)
        {
            await suggestions.RecordAsync(projectId, suggestionId, awareAt is { } at ? Utc(at) : null, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // POST /Projects/DismissSecurityEventSuggestion: US-128 R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> DismissSecurityEventSuggestion(
            Guid projectId, Guid suggestionId, string? reason, [FromServices] Application.Security.SecurityEventSuggestionService suggestions,
            CancellationToken cancellationToken)
        {
            await suggestions.DismissAsync(projectId, suggestionId, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // POST /Projects/MarkSecurityEventDisclosed: US-127 (CRA Annex I, Part II (4))
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> MarkSecurityEventDisclosed(
            Guid projectId, Guid eventId, DateTime? disclosedAt, [FromServices] Application.Security.SecurityEventService events, CancellationToken cancellationToken)
        {
            var at = disclosedAt is { } d ? Utc(d) : _clock.GetUtcNow().UtcDateTime;
            await events.MarkDisclosedAsync(projectId, eventId, at, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // POST /Projects/UpgradeSecurityEvent: US-127 R2–R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.SecurityEvents)]
        public async Task<IActionResult> UpgradeSecurityEvent(
            Guid projectId, Guid eventId, DateTime? reportableAt, string? reason, [FromServices] Application.Security.SecurityEventService events,
            CancellationToken cancellationToken)
        {
            if (reportableAt is not { } at)
            {
                throw new Domain.Common.DomainException("Give the time it became reportable");
            }

            await events.UpgradeAsync(projectId, eventId, Utc(at), reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.SecurityEvents });
        }

        // ---------------------------------------------------------- Methods (US-104)

        // POST /Projects/ChooseRequirementMethods: R2, R4. The checkboxes send the methods that are ticked.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> ChooseRequirementMethods(Guid projectId, List<RequirementMethods> methods, CancellationToken cancellationToken)
        {
            var chosen = methods.Aggregate(Domain.Projects.RequirementMethods.None, (all, method) => all | method);
            await _service.ChangeRequirementMethodsAsync(projectId, chosen, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // ---------------------------------------------------------- Technical documentation (US-056)

        // GET /Projects/TechnicalDocumentation/{id}: a DOCX draft with the headings of CRA Annex VII.
        // For anyone in the organisation; 404 for other organisations and for projects that do not follow the CRA (R6).
        [HttpGet]
        // US-131: format "pdf" gives the same draft as PDF. Every download is logged (R5).
        public async Task<IActionResult> TechnicalDocumentation(
            Guid id, string? format,
            [FromServices] Application.Reports.TechnicalDocumentationQueries drafts,
            [FromServices] Application.Reports.IDocumentWriter writer,
            [FromServices] Application.Reports.IPdfWriter pdfWriter,
            [FromServices] Application.ActivityLog.IActivityLogger log,
            [FromServices] Application.Common.IUnitOfWork unitOfWork,
            [FromServices] Application.Common.ICurrentUser currentUser,
            CancellationToken cancellationToken)
        {
            var draft = await drafts.DraftAsync(id, cancellationToken);
            var project = await _projects.GetAsync(id, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            if (draft is null || project is null || organization is null)
            {
                return NotFound();
            }

            var pdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
            await log.LogAsync(Domain.ActivityLog.ActivityAction.ReportDownloaded, project.Id,
                $"{(pdf ? "PDF" : "Word")} for Technical documentation (draft)", cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (pdf)
            {
                var header = new Application.Reports.PdfHeader(organization.Name, project.Name, "Technical documentation (draft)",
                    _clock.GetUtcNow().UtcDateTime, currentUser.Identity.Name);
                return File(pdfWriter.FromDocument(draft, header), "application/pdf", Path.ChangeExtension(draft.FileName, ".pdf"));
            }

            return File(writer.ToDocx(draft), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", draft.FileName);
        }

        // ---------------------------------------------------------- Traceability matrix (US-052)

        // GET /Projects/Traceability/{id}?sub=cra: the matrix as CSV, for one regulation or all of them.
        // R5: for anyone in the organisation, also the Auditor; 404 for other organisations and unknown regulations.
        [HttpGet]
        // US-120: format "xlsx" (the default) or "csv"; audience "team" (the default), "auditor" or "management".
        // The CSV keeps the US-052 columns for tools that read it; the management version is Excel only.
        public async Task<IActionResult> Traceability(
            Guid id, string? sub, string? format, string? audience,
            [FromServices] Application.Reports.ISpreadsheetWriter spreadsheet,
            [FromServices] Application.Reports.IPdfWriter pdfWriter,
            [FromServices] Application.Security.SecurityEventQueries securityEvents,
            [FromServices] Application.Scanning.ScanService scans,
            [FromServices] Application.ActivityLog.IActivityLogger log,
            [FromServices] Application.Common.IUnitOfWork unitOfWork,
            [FromServices] Application.Common.ICurrentUser currentUser,
            CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(id, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            var compliance = await _complianceQueries.ForProjectAsync(id, cancellationToken);
            if (project is null || organization is null || compliance is null)
            {
                return NotFound();
            }

            var frameworks = compliance.Frameworks
                .Where(f => sub is null || FrameworkNames.Slug(f.Framework.Name) == sub)
                .ToList();
            if (sub is not null && frameworks.Count == 0)
            {
                return NotFound();
            }

            string NameOf(Guid userId) => organization.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";
            var reader = Enum.TryParse<Application.Reports.ReportAudience>(audience, ignoreCase: true, out var chosen) ? chosen : Application.Reports.ReportAudience.Team;
            var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) && reader != Application.Reports.ReportAudience.Management;
            var pdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase); // US-131
            var name = $"traceability-{FrameworkNames.Slug(project.Name)}{(sub is null ? "" : "-" + sub)}"
                       + (csv ? "" : $"-{reader.ToString().ToLowerInvariant()}") + $"-{_clock.Today():yyyy-MM-dd}";

            // R8: every download is logged
            var what = string.Join(", ", frameworks.Select(f => FrameworkNames.Short(f.Framework.Name)));
            await log.LogAsync(Domain.ActivityLog.ActivityAction.ReportDownloaded, project.Id,
                $"{(csv ? "CSV" : pdf ? "PDF" : "Excel")} for {Application.Reports.TraceabilityReport.Title(reader)}: {what}", cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (csv)
            {
                return File(TraceabilityMatrix.ToCsv(TraceabilityMatrix.Rows(frameworks, NameOf)), "text/csv; charset=utf-8", name + ".csv");
            }

            // R6: the management facts
            var now = _clock.GetUtcNow().UtcDateTime;
            var facts = new Application.Reports.ManagementFacts(
                (await _risks.ForProjectAsync(id, cancellationToken) ?? [])
                    .Count(r => r.Status != Domain.Risks.RiskStatus.Closed && r.Level >= Domain.Risks.RiskLevel.High),
                (await scans.ForProjectAsync(id, cancellationToken))?.OpenAtLeastHigh ?? 0,
                (await securityEvents.ForProjectAsync(id, cancellationToken) ?? []).Count(e => e.IsOpen),
                project.SupportEndsOn);
            var document = Application.Reports.TraceabilityReport.Build(reader, project, frameworks, NameOf,
                currentUser.Identity.Name, now, facts);
            if (pdf)
            {
                // US-131 R2: the same report model as the Excel file
                var header = new Application.Reports.PdfHeader(organization.Name, project.Name, document.Title, now, currentUser.Identity.Name);
                return File(pdfWriter.FromReport(document, header), "application/pdf", name + ".pdf");
            }

            return File(spreadsheet.ToXlsx(document), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
        }

        // ---------------------------------------------------------- AI suggestions (US-020, US-036)

        // POST /Projects/SuggestMappings: R1, R8
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> SuggestMappings(Guid projectId, CancellationToken cancellationToken)
        {
            await _suggestions.SuggestAsync(projectId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // POST /Projects/ShowMoreSuggestions: US-036 R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.ProductRequirements)]
        public async Task<IActionResult> ShowMoreSuggestions(Guid projectId, Guid workItemId, CancellationToken cancellationToken)
        {
            await _suggestions.ShowMoreAsync(projectId, workItemId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.ProductRequirements });
        }

        // POST /Projects/SuggestForNotCovered: US-044 R3, R8. Only the open framework's not-covered requirements.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> SuggestForNotCovered(Guid projectId, Guid frameworkId, string? sub, CancellationToken cancellationToken)
        {
            await _suggestions.SuggestForNotCoveredAsync(projectId, frameworkId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // ---------------------------------------------------------- Not applicable, suggested by the AI (US-060, US-061)

        // POST /Projects/SuggestNotApplicable: US-060 R6. Only the open framework's not covered requirements.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> SuggestNotApplicable(
            Guid projectId, Guid frameworkId, string? sub, [FromServices] ApplicabilitySuggestionService applicability, CancellationToken cancellationToken)
        {
            var made = await applicability.SuggestAsync(projectId, frameworkId, cancellationToken);
            TempData["ApplicabilityMessage"] = made == 0
                ? "The AI found no requirement that clearly does not apply"
                : $"The AI suggests {made} {(made == 1 ? "requirement" : "requirements")} as not applicable. Accept or reject each one.";
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/AcceptApplicability: US-060 R6, R8. The reason can be edited before it is accepted.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> AcceptApplicability(
            Guid projectId, Guid suggestionId, string? reason, string? sub, [FromServices] ApplicabilitySuggestionService applicability,
            CancellationToken cancellationToken)
        {
            await applicability.AcceptAsync(projectId, suggestionId, reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/RejectApplicability: US-060 R8
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> RejectApplicability(
            Guid projectId, Guid suggestionId, string? sub, [FromServices] ApplicabilitySuggestionService applicability, CancellationToken cancellationToken)
        {
            await applicability.RejectAsync(projectId, suggestionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/KeepNotApplicable: US-061 R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> KeepNotApplicable(Guid projectId, Guid requirementId, string? sub, CancellationToken cancellationToken)
        {
            await _compliance.KeepNotApplicableAsync(projectId, requirementId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub });
        }

        // POST /Projects/ApproveSuggestion: US-020 R6. US-044 R5: decided on the Compliance tab, back to the same view.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> ApproveSuggestion(Guid projectId, Guid suggestionId, string? sub, string? show, CancellationToken cancellationToken)
        {
            await _suggestions.ApproveAsync(projectId, suggestionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub, show = string.IsNullOrEmpty(show) ? null : show });
        }

        // POST /Projects/RejectSuggestion: US-020 R7. An unknown reason binds as null and is refused by the domain.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Compliance, SubFrom = "sub")]
        public async Task<IActionResult> RejectSuggestion(
            Guid projectId, Guid suggestionId, RejectionReason? reason, string? comment, string? sub, string? show, CancellationToken cancellationToken)
        {
            await _suggestions.RejectAsync(projectId, suggestionId, reason, comment, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Compliance, sub, show = string.IsNullOrEmpty(show) ? null : show });
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

            ViewData["ExcludedFromAiSharing"] = project.ExcludedFromAiSharing; // US-130
            return View(EditProjectViewModel.From(project));
        }

        // POST /Projects/Edit: R1, R3. A broken rule shows the form again with what was typed (IFormModel).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProjectViewModel model, CancellationToken cancellationToken)
        {
            await _service.UpdateAsync(
                model.Id, model.Name ?? "", model.Description, model.IntendedPurpose ?? "", model.Scope, cancellationToken, model.Version);
            return RedirectToAction(nameof(Details), new { id = model.Id }); // Post/Redirect/Get
        }

        // POST /Projects/SetAiSharing: US-130 R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Edit), IdFrom = "projectId")]
        public async Task<IActionResult> SetAiSharing(Guid projectId, bool excluded, CancellationToken cancellationToken)
        {
            await _service.SetExcludedFromAiSharingAsync(projectId, excluded, cancellationToken);
            return RedirectToAction(nameof(Edit), new { id = projectId });
        }

        // POST /Projects/ChangePhase: R2, R4. US-025 R1: releasing sends the support end date (and a reason when short).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> ChangePhase(
            Guid projectId, LifecyclePhase phase, DateOnly? supportEndsOn, string? supportReason,
            DateOnly? lastPlacedOnMarket, DateOnly? keepUntil, CancellationToken cancellationToken)
        {
            // US-074: for End of support, the last day on the market and an optional later retention date
            await _service.ChangePhaseAsync(projectId, phase, supportEndsOn, supportReason, cancellationToken, lastPlacedOnMarket, keepUntil);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST /Projects/SetSupportPeriod: US-025 R2, R3, R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId")]
        public async Task<IActionResult> SetSupportPeriod(
            Guid projectId, DateOnly? supportEndsOn, string? supportReason, CancellationToken cancellationToken)
        {
            if (supportEndsOn is not { } endsOn)
            {
                throw new Domain.Common.DomainException("Choose the support end date");
            }

            await _service.SetSupportPeriodAsync(projectId, endsOn, supportReason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // ---------------------------------------------------------- Frameworks (US-011)

        // POST /Projects/AddFramework: R2, R3. Without a version, the newest is used.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Frameworks)]
        public async Task<IActionResult> AddFramework(Guid projectId, Guid frameworkId, Guid? frameworkVersionId, CancellationToken cancellationToken)
        {
            await _service.AddFrameworkAsync(projectId, frameworkId, frameworkVersionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Frameworks }); // Post/Redirect/Get
        }

        // POST /Projects/UseNewest: R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Frameworks)]
        // US-035 R5: with frameworkVersionId, the project moves to that (e.g. upcoming) version instead
        public async Task<IActionResult> UseNewest(Guid projectId, Guid frameworkId, Guid? frameworkVersionId, CancellationToken cancellationToken)
        {
            await _service.UseNewestVersionAsync(projectId, frameworkId, frameworkVersionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Frameworks });
        }

        // POST /Projects/RemoveFramework: R7
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "projectId", Tab = ProjectTabs.Frameworks)]
        public async Task<IActionResult> RemoveFramework(Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
        {
            await _service.RemoveFrameworkAsync(projectId, frameworkId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = projectId, tab = ProjectTabs.Frameworks });
        }
    }
}
