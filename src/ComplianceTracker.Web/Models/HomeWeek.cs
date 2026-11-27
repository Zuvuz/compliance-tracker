using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Application.Isms;
using ComplianceTracker.Application.Review;

namespace ComplianceTracker.Web.Models
{
    // US-227: one thing on Home: what it is, the project it belongs to (none for the ISMS), when it is due and where it is
    // done. Href is null when the member's role can only read it (US-099 R6).
    public sealed record HomeItem(string Kind, string Title, Guid? ProjectId, string? Project, DateTime Due, string? Href);

    // US-227: "Your week" on Home is the member's tasks (US-099), the ISMS overview's "My week" (US-216) and the approvals
    // that wait for the member (US-135), as one list, overdue first, up to 7 days ahead; the rest is "Show more".
    // Pure, so the edge cases are tested on their own.
    public static class HomeWeek
    {
        public const string Isms = "Isms";
        public const string Approval = "Approval";

        public static (IReadOnlyList<HomeItem> Week, IReadOnlyList<HomeItem> Later) Build(
            IEnumerable<TaskItem> tasks,
            IEnumerable<WeekItem> isms,
            IEnumerable<ApprovalView> approvals,
            DateTime now,
            Func<WeekItem, string> ismsHref,
            Func<ApprovalView, string> approvalHref,
            Guid? project = null,
            TaskKind? kind = null)
        {
            var items = new List<HomeItem>();
            items.AddRange(tasks.Where(t => (project is null || t.ProjectId == project) && (kind is null || t.Kind == kind))
                .Select(t => new HomeItem(t.Kind.ToString(), t.Title, t.ProjectId, t.ProjectName, t.Due, t.CanAct ? t.Path : null)));

            // The filters are about the projects' tasks; the ISMS is not a project, so it is left out when one is chosen
            if (kind is null)
            {
                if (project is null)
                {
                    items.AddRange(isms.Select(i => new HomeItem(Isms, i.Text, null, null, EndOf(i.DueOn), ismsHref(i))));
                }

                // Only what waits for the member: their own requests wait for someone else
                items.AddRange(approvals.Where(a => a.CanApprove && (project is null || a.Request.ProjectId == project))
                    .Select(a => new HomeItem(Approval, $"Approve: {a.Request.Summary}", a.Request.ProjectId, null, a.Request.ExpiresAt, approvalHref(a))));
            }

            var ordered = items.OrderBy(i => i.Due).ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase).ToList();
            return (ordered.Where(i => MyTaskPlan.IsSoon(i.Due, now)).ToList(), ordered.Where(i => !MyTaskPlan.IsSoon(i.Due, now)).ToList());
        }

        // The number on the bell: what is overdue or due within 7 days
        public static int Count(IEnumerable<TaskItem> tasks, IEnumerable<WeekItem> isms, IEnumerable<ApprovalView> approvals, DateTime now)
            => Build(tasks, isms, approvals, now, _ => "", _ => "").Week.Count;

        // An ISMS item is due at the end of its day, so it is overdue from the next day (as on the ISMS overview)
        public static DateTime EndOf(DateOnly day) => day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
    }
}
