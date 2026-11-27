using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Models
{
    // US-219 R1: a page with sensitive data starts a visit when it is opened. The layout then sends a sign of life every
    // 30 seconds while the page is visible, and one when it is hidden or left (wwwroot/js/access-log.js). Requests the page
    // makes by itself are signs of life, never new visits.
    public static class PageVisits
    {
        public const string Key = "PageVisitId";

        // R7: nothing happens while the access log is off
        public static async Task StartVisitAsync(this Controller controller, Guid? projectId, VisitKind kind, Guid? itemId, string? tab, string title,
            CancellationToken cancellationToken, bool once = false)
        {
            var service = controller.HttpContext.RequestServices.GetRequiredService<AccessLogService>();
            if (await service.StartAsync(projectId, kind, itemId, tab, title, cancellationToken, once) is { } id && !once)
            {
                controller.ViewData[Key] = id;
            }
        }

        // R3: the other members on this project page now, for the header
        public const string OnPageNowKey = "OnPageNow";

        public static async Task ShowWhoIsHereAsync(this Controller controller, Guid projectId, string? tab, CancellationToken cancellationToken)
            => controller.ViewData[OnPageNowKey] = await controller.HttpContext.RequestServices.GetRequiredService<AccessLogService>()
                .OnPageNowAsync(projectId, tab, cancellationToken);

        // "Seen by" on a risk or evidence, for the roles that see the access log
        public const string SeenByKey = "SeenBy";

        public static async Task ShowSeenByAsync(this Controller controller, VisitKind kind, Guid itemId, CancellationToken cancellationToken)
            => controller.ViewData[SeenByKey] = await controller.HttpContext.RequestServices.GetRequiredService<AccessLogService>()
                .SeenByAsync(kind, itemId, cancellationToken);

        // The kinds a person can filter by, with their names
        public static string Title(VisitKind kind) => kind switch
        {
            VisitKind.Tab => "Project page",
            VisitKind.SecurityEvent => "Security event",
            VisitKind.AuditPackage => "Audit package",
            _ => kind.ToString()
        };

        // An unknown kind is no filter
        public static VisitKind? ParseKind(string? value)
            => Enum.TryParse<VisitKind>(value, ignoreCase: true, out var kind) && Enum.IsDefined(kind) ? kind : null;

        // The cookie that says the member has read the notice about the access log (R7), per organisation and start date
        public static string NoticeCookie(Guid organizationId, DateOnly startsOn) => $"access-log-notice-{organizationId:N}-{startsOn:yyyyMMdd}";
    }

    public record AccessLogPageModel(
        AccessLogView View,
        Domain.Organizations.Organization Organization,
        IReadOnlyList<Domain.Projects.Project> Projects,
        Guid? ProjectId,
        Guid? UserId,
        VisitKind? Kind,
        DateOnly? From,
        DateOnly? To,
        bool CanSeeChanges,
        DateTime Now);

    // US-219 R4: the Activity pages' "Changes" and "Access"; Project is null on the organisation's pages
    public record LogPills(string Open, bool CanSeeChanges, Domain.Projects.Project? Project, bool CanSeeAccess = true)
    {
        public const string Changes = "changes";
        public const string Access = "access";
    }
}
