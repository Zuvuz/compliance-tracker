using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Web.Models
{
    // US-101, US-102: the activity log's filter bar, table and pages (Views/Shared/_ActivityLog.cshtml),
    // for a project's Activity tab and for the organisation's Activity page.
    // Subjects and Projects are only given on the organisation's page, which shows "About" and the project filter.
    public record ActivityLogViewModel(
        string SectionId,
        string Address,
        ActivityLogQuery Query,
        ActivityLogPage? Page,
        IReadOnlyList<string> Ignored,
        Organization? Organization,
        IReadOnlyDictionary<Guid, ActivitySubject>? Subjects = null,
        IReadOnlyList<Project>? Projects = null,
        string? ProjectFilter = null)
    {
        public const string NotAboutAProject = "none";

        public bool IsOrganisationLog => Subjects is not null;

        public bool HasFilters => Query.HasFilters || !string.IsNullOrEmpty(ProjectFilter);

        // The name of a member, or "System" for what the tracker did by itself (US-018 R6, US-102 R5)
        public string NameOf(Guid? userId)
            => userId is null ? "System" : Organization?.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";

        // US-102 R4: what the entry is about, or null
        public ActivitySubject? About(ActivityLogEntry entry)
            => entry.SubjectId is { } id && Subjects?.TryGetValue(id, out var subject) == true ? subject : null;

        // The address of another page with the same filters
        public string PageLink(int page)
        {
            var values = ActivityFilters.RouteValues(Query, page);
            values["project"] = ProjectFilter;
            var query = string.Join("&", values.Where(v => !string.IsNullOrEmpty(v.Value))
                .Select(v => $"{Uri.EscapeDataString(v.Key)}={Uri.EscapeDataString(v.Value!)}"));
            return query.Length == 0 ? Address : $"{Address}?{query}";
        }
    }
}
