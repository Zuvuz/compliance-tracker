using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Security
{
    // Storage for security events (US-073, implemented in Infrastructure)
    public interface ISecurityEventRepository
    {
        // Newest first
        Task<IReadOnlyList<SecurityEvent>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<SecurityEvent?> GetAsync(Guid projectId, Guid eventId, CancellationToken cancellationToken = default);

        Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);
    }

    // US-135: what an approved "not reportable" event needs; SuggestionId when it came from a suggestion (US-128)
    public record NotReportableEventPayload(
        SecurityEventKind Kind, string Title, string? Description, DateTime AwareAt, Guid? SuggestionId);

    // US-073: record actively exploited vulnerabilities and severe incidents of a released product, mark their
    // reports as sent and close them (CRA Art. 14). The same roles as the risk register (ManageRisks).
    // US-135 R2: an event that is not reportable waits for a second person (the IApprovalExecutor for it).
    public class SecurityEventService : IApprovalExecutor
    {
        private readonly IProjectRepository _projects;
        private readonly ISecurityEventRepository _events;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        private readonly ApprovalGate _approvals;
        private readonly ISecurityEventSuggestionRepository _suggestions;

        public SecurityEventService(
            IProjectRepository projects, ISecurityEventRepository events, CurrentMember member, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock, ApprovalGate approvals, ISecurityEventSuggestionRepository suggestions)
        {
            _approvals = approvals;
            _suggestions = suggestions;
            _projects = projects;
            _events = events;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // US-135 R2: an event that is not reportable waits for a second person; null when it waits
        public async Task<SecurityEvent?> RecordAsync(
            Guid projectId, SecurityEventKind kind, string title, string? description, DateTime awareAtUtc,
            CancellationToken cancellationToken = default)
        {
            var (user, project) = await OpenProjectAsync(projectId, cancellationToken);
            if (IsNotReportable(kind))
            {
                await RequestNotReportableAsync(user, project, new NotReportableEventPayload(kind, title, description, awareAtUtc, null), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return null;
            }

            var securityEvent = await AddAsync(project, kind, title, description, awareAtUtc, user, null, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return securityEvent;
        }

        public static bool IsNotReportable(SecurityEventKind kind)
            => kind is SecurityEventKind.VulnerabilityNotExploited or SecurityEventKind.IncidentNotSevere;

        // US-135: also used by the suggestions (US-128); does not save
        internal async Task RequestNotReportableAsync(User user, Project project, NotReportableEventPayload payload, CancellationToken cancellationToken)
        {
            // The same checks as recording, so a request is never made for an event that cannot be recorded
            SecurityEvent.Record(project.Id, payload.Kind, payload.Title, payload.Description, payload.AwareAt, user.Id, user.Role, Now);
            var organization = (await _member.RequireAsync(cancellationToken)).Organization;
            var kind = payload.Kind == SecurityEventKind.VulnerabilityNotExploited ? "vulnerability, not exploited" : "incident, not severe";
            await _approvals.RequestAsync(
                organization, user, project.Id, ApprovalAction.RecordNotReportableEvent, payload.SuggestionId, null,
                $"Record \"{payload.Title.Trim()}\" as {kind}, so it is not reported", payload, cancellationToken);
        }

        private async Task<SecurityEvent> AddAsync(
            Project project, SecurityEventKind kind, string title, string? description, DateTime awareAtUtc, User user, User? approver,
            CancellationToken cancellationToken)
        {
            var securityEvent = SecurityEvent.Record(project.Id, kind, title, description, awareAtUtc, user.Id, user.Role, Now);
            await _events.AddAsync(securityEvent, cancellationToken);

            var action = kind is SecurityEventKind.ActivelyExploitedVulnerability or SecurityEventKind.VulnerabilityNotExploited
                ? ActivityAction.VulnerabilityRecorded : ActivityAction.IncidentRecorded;
            var details = $"{securityEvent.Title}, aware {securityEvent.AwareAt:dd-MM-yyyy HH:mm} UTC";
            if (approver is null)
            {
                await _log.LogAsync(action, project.Id, details, cancellationToken);
            }
            else
            {
                await _log.LogAsAsync(project.OrganizationId, user, action, project.Id, $"{details} (approved by {approver.Name})", cancellationToken);
            }

            return securityEvent;
        }

        // ---------------------------------------------------------- US-135: the approved event

        ApprovalAction IApprovalExecutor.Action => ApprovalAction.RecordNotReportableEvent;

        Permission IApprovalExecutor.Permission => Permission.ManageRisks;

        async Task<string?> IApprovalExecutor.WhyStaleAsync(ApprovalRequest request, CancellationToken cancellationToken)
        {
            var payload = ApprovalGate.Read<NotReportableEventPayload>(request);
            return payload.SuggestionId is { } id
                   && (await _suggestions.GetAsync(request.ProjectOf, id, cancellationToken))?.IsPending != true
                ? "the suggestion was decided already"
                : null;
        }

        async Task IApprovalExecutor.ExecuteAsync(ApprovalRequest request, Organization organization, User requester, User approver, CancellationToken cancellationToken)
        {
            var payload = ApprovalGate.Read<NotReportableEventPayload>(request);
            var project = await _projects.GetAsync(organization.Id, request.ProjectOf, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var recorded = await AddAsync(project, payload.Kind, payload.Title, payload.Description, payload.AwareAt, requester, approver, cancellationToken);
            if (payload.SuggestionId is { } suggestionId
                && await _suggestions.GetAsync(project.Id, suggestionId, cancellationToken) is { } suggestion)
            {
                // US-128: the suggestion is accepted by the person who asked
                suggestion.Accept(requester.Id, Now);
                suggestion.LinkEvent(recorded.Id);
                await _log.LogAsAsync(organization.Id, requester, ActivityAction.SecurityEventSuggestionAccepted, project.Id,
                    SecurityEventSuggestionService.Describe(suggestion), cancellationToken);
            }
        }

        // R4
        public async Task MarkSentAsync(
            Guid projectId, Guid eventId, SecurityReportKind report, DateTime sentAtUtc, string? reference,
            CancellationToken cancellationToken = default)
        {
            var (_, project) = await OpenProjectAsync(projectId, cancellationToken);
            var securityEvent = await RequireAsync(project, eventId, cancellationToken);

            securityEvent.MarkSent(report, sentAtUtc, reference, Now);
            var late = securityEvent.Status(report, Now) == SecurityReportStatus.SentLate ? " (late)" : "";
            await _log.LogAsync(ActivityAction.SecurityReportSent, project.Id,
                $"{securityEvent.Title}: {SecurityReportNames.Title(report)} sent{late}, ref. {securityEvent.Reference(report)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkFixAvailableAsync(Guid projectId, Guid eventId, DateTime atUtc, CancellationToken cancellationToken = default)
        {
            var (_, project) = await OpenProjectAsync(projectId, cancellationToken);
            var securityEvent = await RequireAsync(project, eventId, cancellationToken);

            securityEvent.MarkFixAvailable(atUtc, Now);
            await _log.LogAsync(ActivityAction.SecurityFixAvailable, project.Id, $"{securityEvent.Title}: fix available", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task CloseAsync(Guid projectId, Guid eventId, string? reason = null, CancellationToken cancellationToken = default)
        {
            var (user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var securityEvent = await RequireAsync(project, eventId, cancellationToken);

            securityEvent.Close(user.Id, Now, reason);
            await _log.LogAsync(ActivityAction.SecurityEventClosed, project.Id,
                securityEvent.Title + (securityEvent.ClosedReason is { } why ? $" ({why})" : ""), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-127: a vulnerability that is not exploited is disclosed after its fix
        public async Task MarkDisclosedAsync(Guid projectId, Guid eventId, DateTime atUtc, CancellationToken cancellationToken = default)
        {
            var (_, project) = await OpenProjectAsync(projectId, cancellationToken);
            var securityEvent = await RequireAsync(project, eventId, cancellationToken);

            securityEvent.MarkDisclosed(atUtc, Now);
            await _log.LogAsync(ActivityAction.SecurityEventDisclosed, project.Id, $"{securityEvent.Title}: disclosed", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-127 R2–R4: from now the reports and deadlines apply, from the time it became reportable
        public async Task UpgradeAsync(Guid projectId, Guid eventId, DateTime reportableAtUtc, string? reason, CancellationToken cancellationToken = default)
        {
            var (user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var securityEvent = await RequireAsync(project, eventId, cancellationToken);

            securityEvent.Upgrade(reportableAtUtc, reason, user.Id, Now);
            await _log.LogAsync(ActivityAction.SecurityEventUpgraded, project.Id,
                $"{securityEvent.Title}: {SecurityReportNames.Title(securityEvent.Kind)} ({securityEvent.UpgradeReason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<SecurityEvent> RequireAsync(Project project, Guid eventId, CancellationToken cancellationToken)
            => await _events.GetAsync(project.Id, eventId, cancellationToken) ?? throw new DomainException("Security event not found");

        private async Task<(User User, Project Project)> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageRisks, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (user, project);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }

    // What the Security events tab and the Overview read: any member of the organisation; null for other organisations
    public class SecurityEventQueries
    {
        private readonly IProjectRepository _projects;
        private readonly ISecurityEventRepository _events;
        private readonly CurrentMember _member;

        public SecurityEventQueries(IProjectRepository projects, ISecurityEventRepository events, CurrentMember member)
        {
            _projects = projects;
            _events = events;
            _member = member;
        }

        public async Task<IReadOnlyList<SecurityEvent>?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is null)
            {
                return null;
            }

            return await _events.ListAsync(projectId, cancellationToken);
        }
    }
}
