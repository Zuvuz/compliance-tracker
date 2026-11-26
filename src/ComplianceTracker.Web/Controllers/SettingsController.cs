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
        public async Task<IActionResult> Index([FromServices] Application.Reports.ReportSigningService signing, CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            // US-163 R3: the admins change the settings; the organisation's own members see them read-only. An auditor and a
            // member limited to one project (US-182) do not see them (US-045 R2).
            var canEdit = await _currentMember.CanAsync(Permission.ManageAiSharing, cancellationToken);
            if (!canEdit)
            {
                var (_, user) = await _currentMember.RequireAsync(cancellationToken);
                if (user.Role == UserRole.Auditor || await _currentMember.OnlyProjectAsync(cancellationToken) is not null)
                {
                    return Forbid();
                }
            }

            ViewData["CanEdit"] = canEdit;
            ViewData["SharedCount"] = canEdit ? await _aiFeedback.SharedCountAsync(cancellationToken) : null; // US-130 R6, for the admins
            ViewData["Seal"] = await signing.SealAsync(cancellationToken); // US-132
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

        // POST /Settings/ChooseIndustry: US-080 R1, R6. US-163 R5: the industry is on the profile now; a form sent to the old
        // address still works and leads there.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Profile", Controller = "Organisation")]
        public async Task<IActionResult> ChooseIndustry(Domain.Organizations.Industry? industry, bool productsUseAi,
            [FromServices] Application.Organizations.IndustryProfileService profiles, CancellationToken cancellationToken)
        {
            await profiles.ChooseAsync(industry, productsUseAi, cancellationToken);
            return RedirectToAction("Profile", "Organisation", null, "industry-profile");
        }

        // POST /Settings/SetReviewDefaults: US-195 R3, the organisation admin only (checked in the use case)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> SetReviewDefaults(int evidenceValidity, int documentReview, int supplierEvidence, int auditCertificate,
            [FromServices] Application.Organizations.ReviewDefaultsService defaults, CancellationToken cancellationToken)
        {
            await defaults.SaveAsync(new Domain.Organizations.ReviewDefaults(evidenceValidity, documentReview, supplierEvidence, auditCertificate), cancellationToken);
            return RedirectToAction(nameof(Index), null, null, "review-defaults");
        }

        // POST /Settings/SetRiskApprovers: US-217 R1, R7, the organisation admin only (checked in the use case)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> SetRiskApprovers(List<UserRole>? roles, [FromServices] Application.Risks.RiskApproverService approvers,
            CancellationToken cancellationToken)
        {
            await approvers.SetAsync(roles, cancellationToken);
            TempData["Success"] = "The roles that approve accepting High and Critical risks are saved.";
            return RedirectToAction(nameof(Index), null, null, "risk-approvers");
        }

        // GET /Settings/Export: US-184 R1, R2, every piece of the organisation's data as one ZIP
        [HttpGet]
        public async Task<IActionResult> Export([FromServices] Application.Organizations.OrganisationExitService exit, CancellationToken cancellationToken)
        {
            var export = await exit.ExportAsync(cancellationToken);
            Response.Headers["X-Content-SHA256"] = export.Sha256;
            return File(export.Content, "application/zip", export.FileName);
        }

        // POST /Settings/StartClosing: US-184 R3, R4, with the name typed to confirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> StartClosing(string? confirmName, [FromServices] Application.Organizations.OrganisationExitService exit,
            CancellationToken cancellationToken)
        {
            await exit.StartClosingAsync(confirmName, cancellationToken);
            return RedirectToAction(nameof(Index), null, null, "close-organisation");
        }

        // POST /Settings/UndoClosing: US-184 R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> UndoClosing([FromServices] Application.Organizations.OrganisationExitService exit, CancellationToken cancellationToken)
        {
            await exit.UndoClosingAsync(cancellationToken);
            return RedirectToAction(nameof(Index), null, null, "close-organisation");
        }

        // POST /Settings/SetCountry: US-142 R6. US-163 R5: on the profile now; the old address still works and leads there.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Profile", Controller = "Organisation")]
        public async Task<IActionResult> SetCountry(string? country, [FromServices] Application.Organizations.OrganisationAreaService area, CancellationToken cancellationToken)
        {
            await area.SetCountryAsync(country ?? "", cancellationToken);
            return RedirectToAction("Profile", "Organisation", null, "organisation-country");
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
