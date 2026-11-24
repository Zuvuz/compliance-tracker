using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Isms;
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
        private readonly CurrentMember _member;

        public IsmsController(IsmsScopeService scope, CurrentMember member)
        {
            _scope = scope;
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

        // The requirement rows of the form: the texts, and the numbers of the rows ticked as handled
        public static IReadOnlyList<(string? Text, bool Handled)> Lines(List<string?>? requirement, List<int>? handled)
            => (requirement ?? []).Select((text, i) => (text, (handled ?? []).Contains(i))).ToList();
    }
}
