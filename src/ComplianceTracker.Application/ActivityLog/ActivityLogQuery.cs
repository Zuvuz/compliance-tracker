using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Application.ActivityLog
{
    // US-101 R3
    public enum ActivitySort
    {
        Newest,
        Oldest,
        User,
        Action
    }

    // US-101: what the user searches for in the activity log. Shared with the organisation's log (US-102).
    // From and To are dates in the user's local time, both included (R2). SystemOnly: entries the system made itself.
    public record ActivityLogQuery(
        string? Search = null,
        DateOnly? From = null,
        DateOnly? To = null,
        ActivityGroup? Group = null,
        Guid? UserId = null,
        bool SystemOnly = false,
        ActivitySort Sort = ActivitySort.Newest,
        int Page = 1,
        // #52: groups left out, e.g. everything but Sign-in. The log itself never changes.
        IReadOnlyList<ActivityGroup>? Hidden = null)
    {
        public const int PageSize = 50;

        public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || From is not null || To is not null
                                  || Group is not null || UserId is not null || SystemOnly || Hidden is { Count: > 0 };

        // The actions the database looks for: the chosen group's, without the hidden groups; null for every action
        public IReadOnlyList<ActivityAction>? Actions()
        {
            if (Group is null && Hidden is not { Count: > 0 })
            {
                return null;
            }

            var hidden = Hidden ?? [];
            return (Group is { } group ? ActivityGroups.ActionsIn(group) : Enum.GetValues<ActivityAction>())
                .Where(a => !hidden.Contains(ActivityGroups.Of(a)))
                .ToList();
        }
    }

    // R4: one page of entries, and how many there are in all
    public record ActivityLogPage(IReadOnlyList<ActivityLogEntry> Entries, int Total, int Page)
    {
        public int First => Total == 0 ? 0 : (Page - 1) * ActivityLogQuery.PageSize + 1;
        public int Last => Math.Min(Page * ActivityLogQuery.PageSize, Total);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Last < Total;
    }

    // The search as the database runs it: times in UTC, a group as its actions, and the users whose name matches the text
    public record ActivityLogSearch(
        Guid OrganizationId,
        Guid? SubjectId,
        string? Text,
        IReadOnlyList<Guid> UsersNamed,
        DateTime? FromUtc,
        DateTime? ToUtc,
        IReadOnlyList<ActivityAction>? Actions,
        Guid? UserId,
        bool SystemOnly,
        ActivitySort Sort,
        int Skip,
        int Take,
        // US-102: entries about these (projects, members, frameworks whose name matches the text) also match the text
        IReadOnlyList<Guid>? SubjectsNamed = null,
        // US-102 R3: leave out entries about these, e.g. every project for "not about a project"
        IReadOnlyList<Guid>? NotSubjects = null);
}
