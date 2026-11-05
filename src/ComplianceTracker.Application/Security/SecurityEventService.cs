using ComplianceTracker.Application.ActivityLog;
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

    // US-073: record actively exploited vulnerabilities and severe incidents of a released product, mark their
    // reports as sent and close them (CRA Art. 14). The same roles as the risk register (ManageRisks).
    public class SecurityEventService
    {
        private readonly IProjectRepository _projects;
        private readonly ISecurityEventRepository _events;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public SecurityEventService(
            IProjectRepository projects, ISecurityEventRepository events, CurrentMember member, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _events = events;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<SecurityEvent> RecordAsync(
            Guid projectId, SecurityEventKind kind, string title, string? description, DateTime awareAtUtc,
            CancellationToken cancellationToken = default)
        {
            var (user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var securityEvent = SecurityEvent.Record(project.Id, kind, title, description, awareAtUtc, user.Id, user.Role, Now);

            await _events.AddAsync(securityEvent, cancellationToken);
            await _log.LogAsync(
                kind == SecurityEventKind.ActivelyExploitedVulnerability ? ActivityAction.VulnerabilityRecorded : ActivityAction.IncidentRecorded,
                project.Id, $"{securityEvent.Title}, aware {securityEvent.AwareAt:dd-MM-yyyy HH:mm} UTC", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return securityEvent;
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

        public async Task CloseAsync(Guid projectId, Guid eventId, CancellationToken cancellationToken = default)
        {
            var (user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var securityEvent = await RequireAsync(project, eventId, cancellationToken);

            securityEvent.Close(user.Id, Now);
            await _log.LogAsync(ActivityAction.SecurityEventClosed, project.Id, securityEvent.Title, cancellationToken);
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
