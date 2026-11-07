using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-010: the tracker owner's pages. All organisations and their members, and role changes.
    // Never an organisation's projects, frameworks or other data. Only for the Keycloak realm role "system-admin".
    [Authorize(Policy = SystemAdmin.Policy)]
    public class SystemController : Controller
    {
        private readonly SystemAdminService _system;
        private readonly Application.Feedback.FeedbackService _feedback;

        public SystemController(SystemAdminService system, Application.Feedback.FeedbackService feedback)
        {
            _system = system;
            _feedback = feedback;
        }

        // US-063 R5: feedback from every organisation; the one thing from an organisation the system admin sees (B42)
        [HttpGet]
        public async Task<IActionResult> Feedback(CancellationToken cancellationToken)
            => View(await _feedback.ListAsync(cancellationToken));

        [HttpGet]
        public async Task<IActionResult> FeedbackItem(Guid id, CancellationToken cancellationToken)
            => await _feedback.GetAsync(id, cancellationToken) is { } feedback ? View(feedback) : NotFound();

        [HttpGet]
        public async Task<IActionResult> FeedbackScreenshot(Guid id, CancellationToken cancellationToken)
            => await _feedback.ScreenshotAsync(id, cancellationToken) is { } shot
                ? File(shot.Content, shot.Feedback.ScreenshotContentType!, shot.Feedback.ScreenshotFileName)
                : NotFound();

        // R6, R7, R10
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(FeedbackItem), IdFrom = "id")]
        public async Task<IActionResult> CreateFeedbackIssue(Guid id, string title, string body, CancellationToken cancellationToken)
        {
            await _feedback.CreateIssueAsync(id, title, body, cancellationToken);
            return RedirectToAction(nameof(FeedbackItem), new { id });
        }

        // R8
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(FeedbackItem), IdFrom = "id")]
        public async Task<IActionResult> DismissFeedback(Guid id, string reason, CancellationToken cancellationToken)
        {
            await _feedback.DismissAsync(id, reason, cancellationToken);
            return RedirectToAction(nameof(FeedbackItem), new { id });
        }

        // ---------------------------------------------------------- AI quality (US-116)

        // POST /System/ExportTrainingData: US-123, JSON Lines written straight to the answer (R4–R7)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(AiQuality))]
        public async Task<IActionResult> ExportTrainingData(
            DateOnly? from, DateOnly? to, string[]? kinds, [FromServices] Application.Ai.TrainingDataExport export, CancellationToken cancellationToken)
        {
            var request = new Application.Ai.TrainingDataRequest(from, to, Application.Ai.TrainingDataExport.Kinds(kinds));
            export.Validate(request);

            Response.ContentType = "application/x-ndjson; charset=utf-8";
            Response.Headers.ContentDisposition = $"attachment; filename=\"{request.FileName}\"";
            await export.WriteAsync(request, Response.Body, cancellationToken);
            return new EmptyResult();
        }

        // GET /System/AiQuality: the runs, newest first, and the rationales to grade
        [HttpGet]
        public async Task<IActionResult> AiQuality([FromServices] Application.Ai.AiEvaluationService evaluations, CancellationToken cancellationToken)
            => View(new Models.AiQualityViewModel(
                await evaluations.ListAsync(cancellationToken),
                await evaluations.RationaleSampleAsync(cancellationToken)));

        // POST /System/EvaluateStoryMatching: R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(AiQuality))]
        public async Task<IActionResult> EvaluateStoryMatching([FromServices] Application.Ai.AiEvaluationService evaluations, CancellationToken cancellationToken)
        {
            await evaluations.EvaluateStoryMatchingAsync(cancellationToken);
            return RedirectToAction(nameof(AiQuality));
        }

        // POST /System/GradeRationales: R3. The form sends "grade-{suggestionId}" = 1..5; empty ones are left out.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(AiQuality))]
        public async Task<IActionResult> GradeRationales([FromServices] Application.Ai.AiEvaluationService evaluations, CancellationToken cancellationToken)
        {
            var grades = Request.Form
                .Where(f => f.Key.StartsWith("grade-", StringComparison.Ordinal) && int.TryParse(f.Value, out _))
                .Where(f => Guid.TryParse(f.Key["grade-".Length..], out _))
                .ToDictionary(f => Guid.Parse(f.Key["grade-".Length..]), f => int.Parse(f.Value!));
            await evaluations.GradeRationalesAsync(grades, cancellationToken);
            return RedirectToAction(nameof(AiQuality));
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
            => View(await _system.ListOrganizationsAsync(cancellationToken));

        [HttpGet]
        public async Task<IActionResult> Organization(Guid id, CancellationToken cancellationToken)
            => await _system.GetOrganizationAsync(id, cancellationToken) is { } organization ? View(organization) : NotFound();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Organization), IdFrom = "id")]
        public async Task<IActionResult> ChangeRole(Guid id, Guid memberId, UserRole role, CancellationToken cancellationToken)
        {
            await _system.ChangeRoleAsync(id, memberId, role, cancellationToken);
            return RedirectToAction(nameof(Organization), new { id });
        }
    }
}
