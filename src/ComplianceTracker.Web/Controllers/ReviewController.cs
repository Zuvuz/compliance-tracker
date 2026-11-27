using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Isms;
using ComplianceTracker.Application.Review;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-169 R3, R4: everything that waits for the signed-in member's decision, across the projects. US-099: "My tasks", the
    // same page with the tasks that have a date (expiring evidence, risk reviews, reporting deadlines, audit findings).
    // US-227: it is Home, the page after sign-in: "Your week" adds the ISMS overview's "My week" and the approvals that
    // wait for the member to the tasks, as one list.
    public class ReviewController : Controller
    {
        private readonly ReviewInbox _inbox;
        private readonly MyTasks _tasks;
        private readonly Application.Projects.ProjectQueries _projects;
        private readonly IsmsOverviewService _isms;
        private readonly ApprovalService _approvals;
        private readonly CurrentMember _member;

        public ReviewController(ReviewInbox inbox, MyTasks tasks, Application.Projects.ProjectQueries projects, IsmsOverviewService isms,
            ApprovalService approvals, CurrentMember member)
        {
            _inbox = inbox;
            _tasks = tasks;
            _projects = projects;
            _isms = isms;
            _approvals = approvals;
            _member = member;
        }

        // GET /Review?project=…&kind=RiskReview. US-099 R3: filtered by project and kind, sorted by due date.
        public async Task<IActionResult> Index(Guid? project, string? kind, CancellationToken cancellationToken)
        {
            // US-086: another organisation's project is not found, like everywhere else
            if (project is { } projectId && await _projects.GetAsync(projectId, cancellationToken) is null)
            {
                return NotFound();
            }

            var all = await _tasks.ForMeAsync(cancellationToken);
            var chosenKind = Enum.TryParse<TaskKind>(kind, true, out var k) && Enum.IsDefined(k) ? k : (TaskKind?)null;
            // US-227: the approvals are in "Your week", so they are not listed again under the decisions
            var decisions = (await _inbox.ForMeAsync(cancellationToken)).Where(d => d.Kind != ReviewKind.Approvals).ToList();
            var (isms, approvals) = await WeekAsync(cancellationToken);
            var (week, later) = HomeWeek.Build(all, isms, approvals, _tasks.Now, i => IsmsLinks.Href(Url, i.Page, i.Fragment), ApprovalHref, project, chosenKind);
            return View(new MyTasksViewModel(
                week,
                later,
                project is null ? decisions : decisions.Where(d => d.ProjectId == project).ToList(),
                all.Select(t => (t.ProjectId, t.ProjectName)).Concat(decisions.Select(d => (d.ProjectId, d.ProjectName)))
                    .Distinct().OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase).ToList(),
                project, chosenKind, _tasks.Now, HomeWeek.Count(all, isms, approvals, _tasks.Now)));
        }

        // GET /Review/Count. US-099 R4, US-227: the number on the bell (overdue or due within 7 days on Home); fetched after
        // the page has loaded, so no page waits for it.
        [HttpGet]
        public async Task<IActionResult> Count(CancellationToken cancellationToken)
        {
            var (isms, approvals) = await WeekAsync(cancellationToken);
            return Json(new { count = HomeWeek.Count(await _tasks.ForMeAsync(cancellationToken), isms, approvals, _tasks.Now) });
        }

        // The ISMS items (this week and later) and the pending approvals; none before the member has an organisation
        private async Task<(IReadOnlyList<WeekItem> Isms, IReadOnlyList<ApprovalView> Approvals)> WeekAsync(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return ([], []);
            }

            var overview = await _isms.ViewAsync(cancellationToken);
            return ([.. overview.MyWeek, .. overview.Later], await _approvals.PendingAsync(null, cancellationToken));
        }

        // Where an approval is decided: a framework version on its own page, everything else on its project's overview
        private string ApprovalHref(ApprovalView approval) => approval.Request.Action == ApprovalAction.ActivateFramework
            ? Url.Action("Version", "Frameworks", new { id = approval.Request.SubjectId }) ?? "#"
            : $"{Url.Action("Details", "Projects", new { id = approval.Request.ProjectId })}#approvals";

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
