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
        public async Task<IActionResult> Index([FromServices] Application.Reports.ReportSigningService signing,
            [FromServices] Application.Organizations.IndustryProfileService industry, CancellationToken cancellationToken)
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

            ViewData["SharedCount"] = await _aiFeedback.SharedCountAsync(cancellationToken); // US-130 R6
            ViewData["Seal"] = await signing.SealAsync(cancellationToken); // US-132
            ViewData["Industry"] = await industry.ViewAsync(cancellationToken); // US-080
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

        // POST /Settings/ChooseIndustry: US-080 R1, R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ChooseIndustry(Domain.Organizations.Industry? industry, bool productsUseAi,
            [FromServices] Application.Organizations.IndustryProfileService profiles, CancellationToken cancellationToken)
        {
            await profiles.ChooseAsync(industry, productsUseAi, cancellationToken);
            return RedirectToAction(nameof(Index), null, null, "industry-profile");
        }

        // POST /Settings/SetCountry: US-142 R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> SetCountry(string? country, [FromServices] Application.Organizations.OrganisationAreaService area, CancellationToken cancellationToken)
        {
            await area.SetCountryAsync(country ?? "", cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/UploadSeal: US-132, the organisation's certificate (.pfx) for sealing signed reports
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1024 * 1024)]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> UploadSeal(
            IFormFile? file, string? password, [FromServices] Application.Reports.ReportSigningService signing, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            if (file is { Length: > 0 })
            {
                await file.CopyToAsync(buffer, cancellationToken);
            }

            await signing.UploadSealAsync(buffer.ToArray(), password, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/RemoveSeal
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> RemoveSeal([FromServices] Application.Reports.ReportSigningService signing, CancellationToken cancellationToken)
        {
            await signing.RemoveSealAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }
    }
}
