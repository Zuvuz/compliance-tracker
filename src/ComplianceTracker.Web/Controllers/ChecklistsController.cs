using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-192: the organisation's checklists under "Organisation": the built-in items per phase and the organisation's own.
    // Signed-in members only (also the FallbackPolicy); changing needs ManageFrameworks, checked in the use case.
    [Authorize]
    public class ChecklistsController : Controller
    {
        private readonly OrganisationChecklistService _checklists;

        public ChecklistsController(OrganisationChecklistService checklists)
        {
            _checklists = checklists;
        }

        // GET /Checklists
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            ViewData["OrganisationArea"] = true;
            return View(await _checklists.ViewAsync(cancellationToken));
        }

        // POST /Checklists/Add: R1, R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Add(LifecyclePhase? phase, string? text, Guid? frameworkId, string? reference, CancellationToken cancellationToken)
        {
            await _checklists.AddAsync(phase, text, frameworkId, reference, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Checklists/Change
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Change(Guid itemId, string? text, string? reference, CancellationToken cancellationToken)
        {
            await _checklists.ChangeAsync(itemId, text, reference, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Checklists/Remove: R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Remove(Guid itemId, CancellationToken cancellationToken)
        {
            await _checklists.RemoveAsync(itemId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }
    }
}
