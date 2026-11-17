using ComplianceTracker.Application.Review;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-169 R3, R4: everything that waits for the signed-in member's decision, across the projects. US-099: "My tasks", the
    // same page with the tasks that have a date (expiring evidence, risk reviews, reporting deadlines, audit findings).
    public class ReviewController : Controller
    {
        private readonly ReviewInbox _inbox;
        private readonly MyTasks _tasks;

        public ReviewController(ReviewInbox inbox, MyTasks tasks)
        {
            _inbox = inbox;
            _tasks = tasks;
        }

        // GET /Review?project=…&kind=RiskReview. US-099 R3: filtered by project and kind, sorted by due date.
        public async Task<IActionResult> Index(Guid? project, string? kind, CancellationToken cancellationToken)
        {
            var all = await _tasks.ForMeAsync(cancellationToken);
            var chosenKind = Enum.TryParse<TaskKind>(kind, true, out var k) && Enum.IsDefined(k) ? k : (TaskKind?)null;
            var decisions = await _inbox.ForMeAsync(cancellationToken);
            return View(new MyTasksViewModel(
                MyTaskPlan.Filter(all, project, chosenKind),
                project is null ? decisions : decisions.Where(d => d.ProjectId == project).ToList(),
                all.Select(t => (t.ProjectId, t.ProjectName)).Concat(decisions.Select(d => (d.ProjectId, d.ProjectName)))
                    .Distinct().OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase).ToList(),
                project, chosenKind, _tasks.Now, MyTaskPlan.Count(all, _tasks.Now)));
        }

        // GET /Review/Count. US-099 R4: the number in the top bar (overdue or due within 7 days); fetched after the page has
        // loaded, so no page waits for it.
        [HttpGet]
        public async Task<IActionResult> Count(CancellationToken cancellationToken)
            => Json(new { count = MyTaskPlan.Count(await _tasks.ForMeAsync(cancellationToken), _tasks.Now) });

        // Where each kind is decided
        public static (string Tab, string? Show) WhereDecided(ReviewKind kind) => kind switch
        {
            ReviewKind.StorySuggestions or ReviewKind.NotApplicable or ReviewKind.StoryDrafts => (ProjectTabs.Compliance, ComplianceFilters.HasSuggestions),
            ReviewKind.NeedsAction => (ProjectTabs.Compliance, ComplianceFilters.NeedsAction),
            ReviewKind.EvidenceChecks => (ProjectTabs.Compliance, null),
            ReviewKind.EvidenceSuggestions => (ProjectTabs.Evidence, null),
            ReviewKind.RiskDrafts or ReviewKind.MeasureSuggestions => (ProjectTabs.Risks, null),
            ReviewKind.SecurityEventSuggestions => (ProjectTabs.SecurityEvents, null),
            ReviewKind.WithoutDate => (ProjectTabs.Evidence, null), // US-195
            _ => (ProjectTabs.Overview, null)
        };
    }
}
