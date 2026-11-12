using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.ActivityLog
{
    // US-102 R3: which entries by project: all, one project, or those that are not about a project
    public enum ActivityProjectScope
    {
        All,
        OneProject,
        NotAboutAProject
    }

    // US-102 R4: what an entry is about, e.g. ("Project", "Acme Portal") or ("Member", "Carl (removed)")
    public record ActivitySubject(string Kind, string Name, Guid Id);

    public record OrganizationActivity(ActivityLogPage Page, IReadOnlyDictionary<Guid, ActivitySubject> Subjects, IReadOnlyList<Project> Projects);

    // US-102: the whole organisation's activity log, for Admins and Auditors (R6). Searches like US-101.
    public class ActivityLogQueries
    {
        private readonly CurrentMember _member;
        private readonly IActivityLogRepository _log;
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;

        public ActivityLogQueries(CurrentMember member, IActivityLogRepository log, IProjectRepository projects, IFrameworkRepository frameworks)
        {
            _member = member;
            _log = log;
            _projects = projects;
            _frameworks = frameworks;
        }

        // R6: only the organisation's Admins and Auditors see the whole log
        public static bool MaySeeAll(UserRole role) => role is UserRole.Admin or UserRole.Auditor;

        public async Task<bool> CanSeeAllAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return false;
            }

            var (_, user) = await _member.RequireAsync(cancellationToken);
            return MaySeeAll(user.Role);
        }

        // One page of the organisation's entries, or null for a member who may not see them
        public Task<OrganizationActivity?> ForOrganizationAsync(
            ActivityLogQuery query, ActivityProjectScope scope, Guid? projectId, CancellationToken cancellationToken = default)
            => SearchAsync(query, scope, projectId, null, cancellationToken);

        // US-055: every entry the filters find, up to "take", oldest first is up to the caller (the query's sort)
        public Task<OrganizationActivity?> AllAsync(
            ActivityLogQuery query, ActivityProjectScope scope, Guid? projectId, int take, CancellationToken cancellationToken = default)
            => SearchAsync(query, scope, projectId, take, cancellationToken);

        private async Task<OrganizationActivity?> SearchAsync(
            ActivityLogQuery query, ActivityProjectScope scope, Guid? projectId, int? take, CancellationToken cancellationToken)
        {
            if (!await CanSeeAllAsync(cancellationToken))
            {
                return null;
            }

            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var projects = await _projects.ListAsync(organization.Id, cancellationToken);
            var subjects = Subjects(organization, projects, await _frameworks.ListAsync(organization.Id, cancellationToken));

            var text = query.Search?.Trim();
            var page = Math.Max(query.Page, 1);
            var named = string.IsNullOrEmpty(text) ? [] : subjects.Values.Where(s => s.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

            var (entries, total) = await _log.SearchAsync(new ActivityLogSearch(
                organization.Id,
                scope == ActivityProjectScope.OneProject ? projectId : null,
                text,
                named.Where(s => s.Kind == "Member").Select(s => s.Id).ToList(),
                query.From is { } from ? from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime() : null,
                query.To is { } to ? to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime() : null,
                query.Group is { } group ? ActivityGroups.ActionsIn(group) : null,
                query.UserId,
                query.SystemOnly,
                query.Sort,
                take is null ? (page - 1) * ActivityLogQuery.PageSize : 0,
                take ?? ActivityLogQuery.PageSize,
                // R1: the search also finds entries about a project, member or framework with that name
                SubjectsNamed: named.Select(s => s.Id).ToList(),
                // R3
                NotSubjects: scope == ActivityProjectScope.NotAboutAProject ? projects.Select(p => p.Id).ToList() : null), cancellationToken);

            return new OrganizationActivity(new ActivityLogPage(entries, total, page), subjects, projects);
        }

        // R4: everything an entry can be about, by id
        private static Dictionary<Guid, ActivitySubject> Subjects(
            Domain.Organizations.Organization organization, IReadOnlyList<Project> projects, IReadOnlyList<Domain.Frameworks.Framework> frameworks)
        {
            var subjects = new Dictionary<Guid, ActivitySubject>
            {
                [organization.Id] = new("Organisation", organization.Name, organization.Id)
            };
            foreach (var project in projects)
            {
                subjects[project.Id] = new("Project", project.Name, project.Id);
            }

            foreach (var user in organization.Users)
            {
                subjects[user.Id] = new("Member", user.IsActive ? user.Name : $"{user.Name} (removed)", user.Id);
            }

            foreach (var framework in frameworks)
            {
                foreach (var version in framework.Versions)
                {
                    subjects[version.Id] = new("Framework", $"{framework.Name} v{version.Number}", framework.Id);
                }
            }

            return subjects;
        }
    }
}
