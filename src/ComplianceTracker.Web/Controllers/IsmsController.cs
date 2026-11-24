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
        private readonly CurrentMember _member;

        public IsmsController(IsmsScopeService scope, AssetService assets, NonconformityService nonconformities, CurrentMember member)
        {
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

        // The requirement rows of the form: the texts, and the numbers of the rows ticked as handled
        public static IReadOnlyList<(string? Text, bool Handled)> Lines(List<string?>? requirement, List<int>? handled)
            => (requirement ?? []).Select((text, i) => (text, (handled ?? []).Contains(i))).ToList();
    }
}
