using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Web.Diagnostics;
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
        public async Task<IActionResult> Index([FromServices] ChecklistSuggestionService suggestions, CancellationToken cancellationToken)
        {
            ViewData["OrganisationArea"] = true;
            ViewData["ChecklistSuggestions"] = await suggestions.PendingAsync(cancellationToken); // US-193
            return View(await _checklists.ViewAsync(cancellationToken));
        }

        // POST /Checklists/Suggest: US-193 R2, R4, for one of the organisation's own frameworks
        [AiRateLimit]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Suggest(Guid frameworkId, [FromServices] ChecklistSuggestionService suggestions, CancellationToken cancellationToken)
        {
            try
            {
                var made = await suggestions.SuggestAsync(frameworkId, cancellationToken);
                TempData["ChecklistAi"] = made == 0 ? "No new requirement fits a phase." : $"The AI suggests {made} checklist item{(made == 1 ? "" : "s")}.";
            }
            catch (Application.Ai.AiUnavailableException e)
            {
                TempData["ChecklistAi"] = e.Message;
            }

            return RedirectToAction(nameof(Index), null, null, "checklist-suggestions");
        }

        // POST /Checklists/ApproveSuggestion: US-193 R2, R3, with the text and phase as the person left them
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ApproveSuggestion(Guid suggestionId, string? text, LifecyclePhase? phase,
            [FromServices] ChecklistSuggestionService suggestions, CancellationToken cancellationToken)
        {
            await suggestions.ApproveAsync(suggestionId, text, phase, cancellationToken);
            return RedirectToAction(nameof(Index), null, null, "checklist-suggestions");
        }

        // POST /Checklists/RejectSuggestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> RejectSuggestion(Guid suggestionId, [FromServices] ChecklistSuggestionService suggestions, CancellationToken cancellationToken)
        {
            await suggestions.RejectAsync(suggestionId, cancellationToken);
            return RedirectToAction(nameof(Index), null, null, "checklist-suggestions");
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
