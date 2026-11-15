using ComplianceTracker.Application.Review;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-169 R3, R4: "To review": everything that waits for the signed-in member's decision, across the projects
    public class ReviewController : Controller
    {
        private readonly ReviewInbox _inbox;

        public ReviewController(ReviewInbox inbox)
        {
            _inbox = inbox;
        }

        // GET /Review
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
            => View(await _inbox.ForMeAsync(cancellationToken));

        // Where each kind is decided
        public static (string Tab, string? Show) WhereDecided(ReviewKind kind) => kind switch
        {
            ReviewKind.StorySuggestions or ReviewKind.NotApplicable or ReviewKind.StoryDrafts => (ProjectTabs.Compliance, ComplianceFilters.HasSuggestions),
            ReviewKind.NeedsAction => (ProjectTabs.Compliance, ComplianceFilters.NeedsAction),
            ReviewKind.EvidenceChecks => (ProjectTabs.Compliance, null),
            ReviewKind.EvidenceSuggestions => (ProjectTabs.Evidence, null),
            ReviewKind.RiskDrafts or ReviewKind.MeasureSuggestions => (ProjectTabs.Risks, null),
            ReviewKind.SecurityEventSuggestions => (ProjectTabs.SecurityEvents, null),
            _ => (ProjectTabs.Overview, null)
        };
    }
}
