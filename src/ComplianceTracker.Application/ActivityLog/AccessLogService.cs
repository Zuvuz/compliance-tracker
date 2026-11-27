using System.Globalization;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.ActivityLog
{
    // One line of the access log, with the names it is shown with
    public record VisitRow(PageVisit Visit, string UserName, string? ProjectName)
    {
        public string Role => RolePermissions.Title(Visit.Role);
    }

    // The access log page: the visits found, who is on a page now (R3), and whether the log is on
    public record AccessLogView(IReadOnlyList<VisitRow> Visits, IReadOnlyList<VisitRow> OnPageNow, bool IsOn, DateOnly? StartsOn, int RetentionMonths, bool Cut);

    // "Seen by" on a risk, a security event or evidence: per member the last time and the total time
    public record SeenBy(string UserName, string Role, DateTime LastSeenAt, TimeSpan Total, int Visits);

    // US-219: who opened what, when and for how long. Visits are logged only while the access log is on (R7); only
    // Organisation admin, Compliance manager and Auditor see them (R4), and each member sees their own.
    public class AccessLogService
    {
        public const int MaxRows = 1000;

        private readonly CurrentMember _member;
        private readonly IPageVisitRepository _visits;
        private readonly IOrganizationRepository _organizations;
        private readonly IProjectRepository _projects;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public AccessLogService(CurrentMember member, IPageVisitRepository visits, IOrganizationRepository organizations, IProjectRepository projects,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _member = member;
            _visits = visits;
            _organizations = organizations;
            _projects = projects;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
        private DateOnly Today => DateOnly.FromDateTime(Now);

        // ---------------------------------------------------------- Logging a visit (R1, R2)

        // R1, R7: starts a visit and returns its id; null when the access log is not on, or for someone who is not a member.
        // once: a download (a report, an audit package) is opened and over at once, with no time spent.
        public async Task<Guid?> StartAsync(Guid? projectId, VisitKind kind, Guid? itemId, string? tab, string title, CancellationToken cancellationToken = default,
            bool once = false)
        {
            var organization = await _member.FindOrganizationAsync(cancellationToken);
            if (organization is null || !organization.IsAccessLogOn(Today))
            {
                return null;
            }

            var (_, user) = await _member.RequireAsync(cancellationToken);
            var visit = PageVisit.Start(organization.Id, user.Id, user.Role, projectId, kind, itemId, tab, title, Now);
            if (once)
            {
                visit.End(Now);
            }

            await _visits.AddAsync(visit, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return visit.Id;
        }

        // R2: a sign of life from the page, or the page saying goodbye. Only the member's own visit, in the organisation
        // shown; a member who was removed while on the page is no longer a member, so nothing changes.
        public async Task<bool> SignOfLifeAsync(Guid visitId, bool visible, bool ended, CancellationToken cancellationToken = default)
        {
            var organization = await _member.FindOrganizationAsync(cancellationToken);
            var visit = await _visits.GetAsync(visitId, cancellationToken);
            if (organization is null || visit is null || visit.OrganizationId != organization.Id)
            {
                return false;
            }

            User user;
            try
            {
                (_, user) = await _member.RequireAsync(cancellationToken);
            }
            catch (DomainException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false; // no longer among the members
            }

            if (visit.UserId != user.Id)
            {
                return false;
            }

            var changed = ended ? visit.End(Now) : visit.SignOfLife(Now, visible);
            if (changed)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return changed;
        }

        // ---------------------------------------------------------- Reading the log (R3, R4, R5)

        // R4: whether the signed-in member sees the access log
        public async Task<bool> CanSeeAsync(CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is not null
               && AccessLogRules.CanSee((await _member.RequireAsync(cancellationToken)).User.Role);

        // R5: the organisation's access log, or one project's; null for a member who may not see it
        public async Task<AccessLogView?> SearchAsync(Guid? projectId, Guid? userId, VisitKind? kind, DateOnly? from, DateOnly? to,
            CancellationToken cancellationToken = default)
        {
            if (!await CanSeeAsync(cancellationToken))
            {
                return null;
            }

            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var found = await _visits.SearchAsync(new AccessLogSearch(organization.Id, projectId, userId, kind,
                from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), MaxRows + 1), cancellationToken);
            var now = await _visits.SeenSinceAsync(organization.Id, projectId, Now - AccessLogRules.EndsAfter, cancellationToken);
            var names = await ProjectNamesAsync(organization, cancellationToken);
            return new AccessLogView(
                Rows(found.Take(MaxRows), organization, names),
                Rows(OnPage(now, Now), organization, names),
                organization.IsAccessLogOn(Today), organization.AccessLogStartsOn, organization.AccessLogRetentionMonths, found.Count > MaxRows);
        }

        // R3: the other members on a project's page now, e.g. "Bo – on the page now (3 min)"; empty for those who may not see it
        public async Task<IReadOnlyList<VisitRow>> OnPageNowAsync(Guid projectId, string? tab, CancellationToken cancellationToken = default)
        {
            if (!await CanSeeAsync(cancellationToken))
            {
                return [];
            }

            var (organization, me) = await _member.RequireAsync(cancellationToken);
            if (!organization.IsAccessLogOn(Today))
            {
                return []; // no one sends signs of life while it is off
            }

            var seen = await _visits.SeenSinceAsync(organization.Id, projectId, Now - AccessLogRules.EndsAfter, cancellationToken);
            var here = OnPage(seen.Where(v => v.UserId != me.Id && (tab is null || v.Tab == tab)), Now);
            return Rows(here, organization, new Dictionary<Guid, string>());
        }

        // "Seen by" on one item; null for a member who may not see the access log
        public async Task<IReadOnlyList<SeenBy>?> SeenByAsync(VisitKind kind, Guid itemId, CancellationToken cancellationToken = default)
        {
            if (!await CanSeeAsync(cancellationToken))
            {
                return null;
            }

            var (organization, _) = await _member.RequireAsync(cancellationToken);
            return Summary(await _visits.ForItemAsync(organization.Id, kind, itemId, cancellationToken), organization.Users);
        }

        // GDPR art. 15: the signed-in member's own visits, whatever their role
        public async Task<IReadOnlyList<VisitRow>> MineAsync(CancellationToken cancellationToken = default)
        {
            var organization = await _member.FindOrganizationAsync(cancellationToken);
            if (organization is null)
            {
                return [];
            }

            var (_, me) = await _member.RequireAsync(cancellationToken);
            var names = await ProjectNamesAsync(organization, cancellationToken);
            return Rows(await _visits.ForUserAsync(organization.Id, me.Id, MaxRows, cancellationToken), organization, names);
        }

        // R5: the organisation's projects for the filter, the organisation area first
        public async Task<IReadOnlyList<Domain.Projects.Project>> ProjectsAsync(CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? (await _projects.ListAsync(organization.Id, cancellationToken)).OrderBy(p => p.IsOrganisationArea ? 0 : 1).ThenBy(p => p.Name).ToList()
                : [];

        // R8: the visits of one project in a period, for the audit package and the export; no check of the role here
        public async Task<IReadOnlyList<VisitRow>> ForPeriodAsync(Organization organization, Guid? projectId, DateOnly from, DateOnly to,
            CancellationToken cancellationToken = default)
        {
            var found = await _visits.SearchAsync(new AccessLogSearch(organization.Id, projectId, null, null,
                from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), 100_000), cancellationToken);
            return Rows(found.OrderBy(v => v.StartedAt), organization, await ProjectNamesAsync(organization, cancellationToken));
        }

        // R8: the columns of the access log in the reports
        public static readonly string[] Columns = ["Opened (UTC)", "Last seen (UTC)", "Time spent (min)", "Person", "Role", "Project", "What"];

        public static IEnumerable<IReadOnlyList<string>> CsvRows(IEnumerable<VisitRow> rows)
            => rows.Select(r => (IReadOnlyList<string>)
            [
                r.Visit.StartedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                r.Visit.LastSeenAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                Math.Round(r.Visit.TimeSpent.TotalMinutes, 1).ToString("0.0", CultureInfo.InvariantCulture),
                r.UserName,
                r.Role,
                r.ProjectName ?? "",
                r.Visit.Title
            ]);

        // ---------------------------------------------------------- Turning it on and off (R6, R7)

        // R7: by the organisation admin, with a start date at least 6 weeks ahead; logged
        public async Task TurnOnAsync(DateOnly startsOn, int retentionMonths, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            organization.TurnOnAccessLog(startsOn, retentionMonths, Today);
            await _log.LogAsync(ActivityAction.AccessLogTurnedOn, organization.Id,
                $"Starts on {organization.AccessLogStartsOn:dd-MM-yyyy}, kept for {organization.AccessLogRetentionMonths} months", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R7: off at once
        public async Task TurnOffAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            if (organization.AccessLogStartsOn is null)
            {
                return;
            }

            organization.TurnOffAccessLog();
            await _log.LogAsync(ActivityAction.AccessLogTurnedOff, organization.Id, "The access log is off", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R6: deletes every organisation's visits older than its retention period; the count goes in the change log.
        // Run by the tracker itself once a day. Returns how many were deleted in all.
        public async Task<int> CleanUpAsync(CancellationToken cancellationToken = default)
        {
            var total = 0;
            foreach (var organization in await _organizations.ListAllAsync(cancellationToken))
            {
                var deleted = await _visits.DeleteStartedBeforeAsync(organization.Id,
                    AccessLogRules.DeleteBefore(Now, organization.AccessLogRetentionMonths), cancellationToken);
                if (deleted > 0)
                {
                    await _log.LogSystemAsync(organization.Id, ActivityAction.AccessLogCleanedUp, organization.Id,
                        $"Deleted {deleted} {(deleted == 1 ? "visit" : "visits")} older than {organization.AccessLogRetentionMonths} months", cancellationToken);
                    total += deleted;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return total;
        }

        // ---------------------------------------------------------- Helpers

        // R3: one line per member and page; the newest visit wins when a member has the same page open twice
        public static IReadOnlyList<PageVisit> OnPage(IEnumerable<PageVisit> visits, DateTime now)
            => visits.Where(v => v.IsOnPage(now))
                .GroupBy(v => (v.UserId, v.ProjectId, v.Kind, v.ItemId, v.Tab))
                .Select(g => g.OrderByDescending(v => v.LastSeenAt).First())
                .OrderBy(v => v.StartedAt)
                .ToList();

        // Per member: the last time seen and the time spent, two tabs at the same time counted once
        public static IReadOnlyList<SeenBy> Summary(IEnumerable<PageVisit> visits, IEnumerable<User> users)
        {
            var people = users.ToDictionary(u => u.Id);
            return visits.GroupBy(v => v.UserId)
                .Select(g => new SeenBy(
                    people.TryGetValue(g.Key, out var user) ? user.Name : "A former member",
                    RolePermissions.Title(g.OrderByDescending(v => v.StartedAt).First().Role),
                    g.Max(v => v.LastSeenAt),
                    AccessLogRules.TotalTime(g),
                    g.Count()))
                .OrderByDescending(s => s.LastSeenAt)
                .ToList();
        }

        private static IReadOnlyList<VisitRow> Rows(IEnumerable<PageVisit> visits, Organization organization, IReadOnlyDictionary<Guid, string> projects)
        {
            var people = organization.Users.ToDictionary(u => u.Id, u => u.Name);
            return visits.Select(v => new VisitRow(v, people.GetValueOrDefault(v.UserId, "A former member"),
                v.ProjectId is { } id ? projects.GetValueOrDefault(id) : null)).ToList();
        }

        private async Task<IReadOnlyDictionary<Guid, string>> ProjectNamesAsync(Organization organization, CancellationToken cancellationToken)
            => (await _projects.ListAsync(organization.Id, cancellationToken)).ToDictionary(p => p.Id, p => p.IsOrganisationArea ? "Organisation" : p.Name);
    }
}
