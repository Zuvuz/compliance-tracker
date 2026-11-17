using ComplianceTracker.Application.Review;

namespace ComplianceTracker.Web.Models
{
    // US-099: "My tasks": the tasks with a date (filtered and sorted), what waits for a decision (US-169), the projects to
    // filter by, the chosen filters, and how many tasks are overdue or due within 7 days (before the filter)
    public sealed record MyTasksViewModel(
        IReadOnlyList<TaskItem> Tasks,
        IReadOnlyList<ReviewItem> Decisions,
        IReadOnlyList<(Guid Id, string Name)> Projects,
        Guid? Project,
        TaskKind? Kind,
        DateTime Now,
        int Soon)
    {
        public bool Filtered => Project is not null || Kind is not null;
    }
}
