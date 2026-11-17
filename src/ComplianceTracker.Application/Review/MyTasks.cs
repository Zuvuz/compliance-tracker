using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Review
{
    // US-099 R1: the kinds of tasks with a date. What waits for a decision (US-169) is listed beside them.
    public enum TaskKind
    {
        EvidenceExpires,
        RiskReview,
        RiskOwned,
        ReportingDeadline,
        FindingDue
    }

    // One task: what, in which project, when it is due and where it is handled. CanAct is false when the member's role
    // cannot handle it (R6): the Auditor sees the task without a link.
    public sealed record TaskItem(Guid ProjectId, string ProjectName, TaskKind Kind, Guid SubjectId, string Title, DateTime Due, string Path, bool CanAct);

    // US-099: the rules of the task list. Pure, so the edge cases are tested on their own.
    public static class MyTaskPlan
    {
        // R4: the top bar counts what is overdue or due within this many days
        public const int SoonDays = 7;

        public static bool IsSoon(DateTime due, DateTime now) => due <= now.AddDays(SoonDays);

        public static bool IsOverdue(DateTime due, DateTime now) => due < now;

        // R4: e.g. "My tasks (3)"
        public static int Count(IEnumerable<TaskItem> tasks, DateTime now) => tasks.Count(t => IsSoon(t.Due, now));

        // R1: the date a risk should be reviewed by (US-042 R6)
        public static DateTime ReviewDue(Risk risk) => (risk.LastReviewedAt ?? risk.CreatedAt).AddMonths(Risk.ReviewMonths);

        // R6: the right a member needs to handle each kind
        public static Permission RightFor(TaskKind kind) => kind switch
        {
            TaskKind.EvidenceExpires => Permission.LinkWork,
            TaskKind.FindingDue => Permission.LinkWork,
            _ => Permission.ManageRisks
        };

        public static string KindName(TaskKind kind) => kind switch
        {
            TaskKind.EvidenceExpires => "Evidence that expires",
            TaskKind.RiskReview => "Risk reviews",
            TaskKind.RiskOwned => "Risks I own",
            TaskKind.ReportingDeadline => "Reporting deadlines",
            _ => "Audit findings"
        };

        // R3: filtered by project and kind, sorted by due date (the earliest first), then by title
        public static IReadOnlyList<TaskItem> Filter(IEnumerable<TaskItem> tasks, Guid? project, TaskKind? kind)
            => tasks.Where(t => (project is null || t.ProjectId == project) && (kind is null || t.Kind == kind))
                .OrderBy(t => t.Due).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();

        public static string ReportName(SecurityReportKind report) => report switch
        {
            SecurityReportKind.EarlyWarning => "Early warning",
            SecurityReportKind.Notification => "Notification",
            _ => "Final report"
        };

        // e.g. "Overdue by 3 days", "Due today", "Due in 5 days", "Due on 01-12-2026"
        public static string DueText(DateTime due, DateTime now)
        {
            var days = (DateOnly.FromDateTime(due).DayNumber - DateOnly.FromDateTime(now).DayNumber);
            return days switch
            {
                < -1 => $"Overdue by {-days} days",
                -1 => "Overdue by 1 day",
                0 => "Due today",
                1 => "Due tomorrow",
                <= SoonDays => $"Due in {days} days",
                _ => $"Due on {due:dd-MM-yyyy}"
            };
        }

        // R1: the risks of one project: those whose review is due within a week, and the open risks the member owns
        public static IEnumerable<(TaskKind Kind, Risk Risk, DateTime Due)> Risks(IEnumerable<Risk> risks, Guid me, DateTime now)
        {
            foreach (var risk in risks.Where(r => r.Status != RiskStatus.Closed))
            {
                var due = ReviewDue(risk);
                if (IsSoon(due, now))
                {
                    yield return (TaskKind.RiskReview, risk, due);
                }
                else if (risk.OwnerId == me)
                {
                    yield return (TaskKind.RiskOwned, risk, due);
                }
            }
        }
    }

    // US-099 R1, R5: the tasks with a date across the projects the member can see; closed projects are left out.
    // A fixed number of queries per project, like "To review" (US-169).
    public class MyTasks
    {
        private readonly IProjectRepository _projects;
        private readonly IEvidenceRepository _evidence;
        private readonly IRiskRepository _risks;
        private readonly ISecurityEventRepository _events;
        private readonly IAuditRepository _audits;
        private readonly CurrentMember _member;
        private readonly TimeProvider _clock;

        public MyTasks(IProjectRepository projects, IEvidenceRepository evidence, IRiskRepository risks, ISecurityEventRepository events,
            IAuditRepository audits, CurrentMember member, TimeProvider clock)
        {
            _projects = projects;
            _evidence = evidence;
            _risks = risks;
            _events = events;
            _audits = audits;
            _member = member;
            _clock = clock;
        }

        public DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<TaskItem>> ForMeAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return [];
            }

            var (organization, me) = await _member.RequireAsync(cancellationToken);

            var now = Now;
            var today = DateOnly.FromDateTime(now);
            var only = await _member.OnlyProjectAsync(cancellationToken); // US-182
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken))
                .Where(p => !p.IsClosed && (only is null || p.Id == only)).ToList();
            var can = new Dictionary<TaskKind, bool>();
            foreach (var kind in Enum.GetValues<TaskKind>())
            {
                can[kind] = await _member.CanAsync(MyTaskPlan.RightFor(kind), cancellationToken);
            }

            var tasks = new List<TaskItem>();
            foreach (var project in projects)
            {
                var name = project.Name;
                var at = $"/Projects/Details/{project.Id}";
                TaskItem Item(TaskKind kind, Guid id, string title, DateTime due, string path) => new(project.Id, name, kind, id, title, due, path, can[kind]);

                // Evidence that has expired or expires within a week; an organisation document's evidence follows the document
                foreach (var evidence in await _evidence.ListActiveAsync(project.Id, cancellationToken))
                {
                    if (evidence.ValidUntil is { } until && evidence.OrganisationDocumentId is null && MyTaskPlan.IsSoon(until.ToDateTime(TimeOnly.MinValue), now))
                    {
                        tasks.Add(Item(TaskKind.EvidenceExpires, evidence.Id,
                            evidence.IsExpiredOn(today) ? $"Evidence \"{evidence.Title}\" has expired" : $"Evidence \"{evidence.Title}\" expires",
                            until.ToDateTime(TimeOnly.MinValue), at + "/evidence"));
                    }
                }

                foreach (var (kind, risk, due) in MyTaskPlan.Risks(await _risks.ListAsync(project.Id, cancellationToken), me.Id, now))
                {
                    tasks.Add(Item(kind, risk.Id, kind == TaskKind.RiskReview ? $"Review the risk \"{risk.Title}\"" : $"Your risk \"{risk.Title}\"",
                        due, $"{at}/risks?risk={risk.Id}"));
                }

                // R1: the reports of a security event that are not sent yet (US-073)
                foreach (var securityEvent in (await _events.ListAsync(project.Id, cancellationToken)).Where(e => e.IsOpen && e.IsReportable && !e.RecordedInError))
                {
                    foreach (var report in Enum.GetValues<SecurityReportKind>())
                    {
                        if (securityEvent.SentAt(report) is null && securityEvent.DueAt(report) is { } deadline)
                        {
                            tasks.Add(Item(TaskKind.ReportingDeadline, securityEvent.Id, $"{MyTaskPlan.ReportName(report)} for \"{securityEvent.Title}\"",
                                deadline, at + "/security-events"));
                            break; // only the next report of each event
                        }
                    }
                }
            }

            // The open audit findings the member owns (US-151)
            foreach (var audit in await _audits.ListAsync(projects.Select(p => p.Id).ToList(), cancellationToken))
            {
                foreach (var finding in audit.Findings.Where(f => f.IsOpen && f.OwnerId == me.Id))
                {
                    var project = projects.First(p => p.Id == finding.ProjectId);
                    tasks.Add(new TaskItem(project.Id, project.Name, TaskKind.FindingDue, finding.Id, $"Audit finding \"{finding.Title}\"",
                        finding.DueOn.ToDateTime(TimeOnly.MinValue), $"/Audits?projectId={project.Id}", can[TaskKind.FindingDue]));
                }
            }

            return MyTaskPlan.Filter(tasks, null, null);
        }
    }
}
