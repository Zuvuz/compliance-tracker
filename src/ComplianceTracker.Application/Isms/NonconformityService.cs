using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Audits;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-212: storage for the nonconformities (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface INonconformityRepository
    {
        Task<IReadOnlyList<Nonconformity>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(Nonconformity nonconformity, CancellationToken cancellationToken = default);
    }

    // Something in a project a nonconformity can come from: an open audit finding or an open security event
    public sealed record NonconformityOrigin(string Key, Guid Id, NonconformitySource Source, string Title, string Project);

    public sealed record NonconformityFilter(NonconformityStatus? Status = null, NonconformitySource? Source = null, bool ShowClosed = false);

    public sealed record NonconformitiesView(
        IReadOnlyList<Nonconformity> Nonconformities,
        NonconformityFilter Filter,
        IReadOnlyList<NonconformityOrigin> Origins,
        IReadOnlyDictionary<Guid, string> Members,
        IReadOnlyDictionary<NonconformityStatus, int> Counts,
        int Overdue,
        bool CanChange,
        DateOnly Today)
    {
        public string NameOf(Guid? id) => id is { } known ? Members.GetValueOrDefault(known) ?? "Former member" : "";
    }

    public class NonconformityService
    {
        private readonly INonconformityRepository _nonconformities;
        private readonly IProjectRepository _projects;
        private readonly IAuditRepository _audits;
        private readonly ISecurityEventRepository _events;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public NonconformityService(INonconformityRepository nonconformities, IProjectRepository projects, IAuditRepository audits,
            ISecurityEventRepository events, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _nonconformities = nonconformities;
            _projects = projects;
            _audits = audits;
            _events = events;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // Every member reads the register
        public async Task<NonconformitiesView> ViewAsync(NonconformityFilter? filter = null, CancellationToken cancellationToken = default)
        {
            filter ??= new NonconformityFilter();
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var all = await _nonconformities.ListAsync(organization.Id, cancellationToken);
            var open = all.Where(n => !n.IsClosed).Select(n => n.SourceId).OfType<Guid>().ToHashSet();
            var origins = (await OriginsAsync(organization.Id, cancellationToken)).Where(o => !open.Contains(o.Id)).ToList();
            return new NonconformitiesView(Filter(all, filter, Today), filter, origins,
                organization.Users.ToDictionary(u => u.Id, u => u.Name),
                Enum.GetValues<NonconformityStatus>().ToDictionary(s => s, s => all.Count(n => n.Status == s)),
                all.Count(n => n.IsOverdue(Today)),
                await _member.CanAsync(Permission.DecideCompliance, cancellationToken), Today);
        }

        // The filters and the order: the overdue first, then the open by severity, then the newest
        public static IReadOnlyList<Nonconformity> Filter(IEnumerable<Nonconformity> all, NonconformityFilter filter, DateOnly today)
            => all
                .Where(n => filter.Status is { } status ? n.Status == status : filter.ShowClosed || !n.IsClosed)
                .Where(n => filter.Source is null || n.Source == filter.Source)
                .OrderBy(n => n.IsClosed)
                .ThenByDescending(n => n.IsOverdue(today))
                .ThenBy(n => n.Severity)
                .ThenByDescending(n => n.RaisedAt)
                .ToList();

        // R1, R6. With an origin ("finding:{id}" or "event:{id}") the source and an empty title come from it.
        public async Task<Nonconformity> RaiseAsync(string? title, string? description, NonconformitySource? source, NonconformitySeverity? severity,
            string? origin, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            Guid? sourceId = null;
            if (!string.IsNullOrWhiteSpace(origin))
            {
                var found = (await OriginsAsync(organization.Id, cancellationToken)).FirstOrDefault(o => o.Key == origin)
                            ?? throw new DomainException("Choose an open audit finding or security event from the list");
                sourceId = found.Id;
                source = found.Source;
                title = string.IsNullOrWhiteSpace(title) ? found.Title : title;
                if ((await _nonconformities.ListAsync(organization.Id, cancellationToken)).Any(n => !n.IsClosed && n.SourceId == found.Id))
                {
                    throw new DomainException($"{found.Title} already has an open nonconformity");
                }
            }

            var nonconformity = Nonconformity.Raise(organization.Id, title, description, source, sourceId, severity, user.Id, Now);
            await _nonconformities.AddAsync(nonconformity, cancellationToken);
            await _log.LogAsync(ActivityAction.NonconformityChanged, organization.Id,
                $"Raised {nonconformity.Title} ({nonconformity.Severity}, from {Label(nonconformity.Source)})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return nonconformity;
        }

        public Task AnalyseAsync(Guid id, string? correction, string? cause, CancellationToken cancellationToken = default)
            => ChangeAsync(id, n =>
            {
                var changed = n.Analyse(correction, cause);
                return changed.Count == 0 ? null : $"{n.Title}: {string.Join(", ", changed)} recorded";
            }, cancellationToken);

        public async Task PlanActionAsync(Guid id, string? action, Guid? ownerId, DateOnly? dueOn, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            if (ownerId is { } owner && organization.Members.All(m => m.Id != owner))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }

            await ChangeAsync(id, n =>
            {
                n.PlanAction(action, ownerId, dueOn, Today);
                return $"{n.Title}: corrective action planned, due {dueOn:dd-MM-yyyy}";
            }, cancellationToken);
        }

        public Task MarkActionDoneAsync(Guid id, CancellationToken cancellationToken = default)
            => ChangeAsync(id, n =>
            {
                n.MarkActionDone(Now);
                return $"{n.Title}: corrective action done";
            }, cancellationToken);

        // R3, R4. Returns true when the nonconformity closed.
        public async Task<bool> CheckAsync(Guid id, string? result, bool effective, CancellationToken cancellationToken = default)
        {
            var (_, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var closed = false;
            await ChangeAsync(id, n =>
            {
                closed = n.Check(result, effective, Today, user.Id, Now);
                return closed ? $"{n.Title}: effective, closed" : $"{n.Title}: not effective, opened again";
            }, cancellationToken);
            return closed;
        }

        // R2
        public Task CloseAsync(Guid id, CancellationToken cancellationToken = default)
            => ChangeAsync(id, n =>
            {
                n.Close();
                return null;
            }, cancellationToken);

        public Task ReopenAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
            => ChangeAsync(id, n =>
            {
                n.Reopen(reason, Today);
                return $"{n.Title}: opened again";
            }, cancellationToken);

        public static string Label(NonconformitySource source) => source switch
        {
            NonconformitySource.InternalAudit => "Internal audit",
            NonconformitySource.ExternalAudit => "External audit",
            NonconformitySource.SecurityEvent => "Security event",
            NonconformitySource.MissedDeadline => "Missed deadline",
            NonconformitySource.ManagementReview => "Management review",
            _ => source.ToString()
        };

        public static string Label(NonconformityStatus status) => status switch
        {
            NonconformityStatus.ActionPlanned => "Action planned",
            NonconformityStatus.ActionDone => "Action done",
            NonconformityStatus.Effective => "Effective",
            _ => "Open"
        };

        // The log line comes back from the change; null when nothing is logged
        private async Task ChangeAsync(Guid id, Func<Nonconformity, string?> change, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var nonconformity = (await _nonconformities.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(n => n.Id == id)
                                ?? throw new DomainException("Nonconformity not found");
            if (change(nonconformity) is not { } detail)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.NonconformityChanged, organization.Id, detail, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<NonconformityOrigin>> OriginsAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            var projects = (await _projects.ListAsync(organizationId, cancellationToken)).Where(p => !p.IsClosed).ToList();
            var names = projects.ToDictionary(p => p.Id, p => p.Name);
            var origins = new List<NonconformityOrigin>();
            foreach (var audit in await _audits.ListAsync(projects.Select(p => p.Id).ToList(), cancellationToken))
            {
                origins.AddRange(audit.Findings.Where(f => f.IsOpen).Select(f => new NonconformityOrigin($"finding:{f.Id}", f.Id,
                    audit.Kind == AuditKind.External ? NonconformitySource.ExternalAudit : NonconformitySource.InternalAudit, f.Title,
                    names.GetValueOrDefault(audit.ProjectId, ""))));
            }

            foreach (var project in projects)
            {
                origins.AddRange((await _events.ListAsync(project.Id, cancellationToken)).Where(e => e.IsOpen)
                    .Select(e => new NonconformityOrigin($"event:{e.Id}", e.Id, NonconformitySource.SecurityEvent, e.Title, project.Name)));
            }

            return origins.OrderBy(o => o.Project, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
