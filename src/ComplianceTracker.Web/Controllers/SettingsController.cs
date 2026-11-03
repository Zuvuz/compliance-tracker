using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-045: Settings in the top bar, for the organisation admin. For now the AI feedback consent and export (US-036).
    // The use cases check the permission again on the server (US-010 R5).
    public class SettingsController : Controller
    {
        private readonly OrganizationQueries _organizations;
        private readonly AiFeedbackService _aiFeedback;
        private readonly CurrentMember _currentMember;

        public SettingsController(OrganizationQueries organizations, AiFeedbackService aiFeedback, CurrentMember currentMember)
        {
            _organizations = organizations;
            _aiFeedback = aiFeedback;
            _currentMember = currentMember;
        }

        // GET /Settings
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            if (!await _currentMember.CanAsync(Permission.ManageAiSharing, cancellationToken))
            {
                return Forbid(); // US-045 R2
            }

            return View(organization);
        }

        // POST /Settings/SetAiSharing: US-036 R5, R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> SetAiSharing(bool share, CancellationToken cancellationToken)
        {
            await _aiFeedback.SetSharingAsync(share, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/ExportAiFeedback: US-036 R7–R9. JSON Lines, one decision per line, without names.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ExportAiFeedback(CancellationToken cancellationToken)
        {
            var lines = await _aiFeedback.ExportAsync(cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(lines), "application/x-ndjson", "ai-feedback.jsonl");
        }
    }
}
