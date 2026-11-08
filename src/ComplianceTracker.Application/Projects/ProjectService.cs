using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;


namespace ComplianceTracker.Application.Projects
{
    // US-135: what an approved end of support needs
    public record EndSupportPayload(DateOnly LastPlacedOnMarket, DateOnly? KeepUntil);

    // Use cases that change projects: create a project (US-008), choose its frameworks (US-011),
    // and edit it or change its lifecycle phase (US-024).
    // The domain checks the fields and the rules about one project; this class checks the rules
    // that need the database and saves the changes and their log entries together.
    // Always acts for the signed-in member (CurrentMember); reading is done by ProjectQueries.
    public class ProjectService : IApprovalExecutor
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly VersionCarryOver _carryOver;
        private readonly Security.ISecurityEventRepository _securityEvents;
        private readonly ApprovalGate _approvals;
        private readonly Organizations.OrganisationDocumentService _documents;

        public ProjectService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock,
            VersionCarryOver carryOver,
            Security.ISecurityEventRepository securityEvents,
            ApprovalGate approvals,
            Organizations.OrganisationDocumentService documents)
        {
            _approvals = approvals;
            _documents = documents;
            _securityEvents = securityEvents;
            _carryOver = carryOver;
            _projects = projects;
            _frameworks = frameworks;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // US-008: creates a project. US-011 R2: the newest version of each chosen framework is added at once.
        public async Task<Project> CreateAsync(
            string name,
            string? description,
            string intendedPurpose,
            string? scope,
            LifecyclePhase phase,
            IReadOnlyCollection<Guid>? frameworkIds = null,
            CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);

            // 1. Validate the fields (R2)
            var project = Project.Create(organization.Id, name, description, intendedPurpose, scope, phase);

            // 2. R3: the name is unique within the organisation
            if (await _projects.NameExistsAsync(organization.Id, project.Name, cancellationToken: cancellationToken))
            {
                throw new DomainException("Project name already in use");
            }

            // 3. Register the project and the log entry (R6)
            var now = Now;
            await _projects.AddAsync(project, cancellationToken);
            await _log.LogAsync(ActivityAction.ProjectCreated, project.Id, cancellationToken);

            // 4. US-011 R1, R2: the chosen frameworks, each logged
            foreach (var frameworkId in (frameworkIds ?? []).Distinct())
            {
                var framework = await AvailableFrameworkAsync(organization.Id, frameworkId, cancellationToken);
                project.UseFramework(framework, framework.RequireCurrentVersion(_clock.Today()), user.Id, now);
                await _log.LogAsync(ActivityAction.ProjectFrameworkAdded, project.Id, cancellationToken);
            }

            // 5. Save it all at once
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SyncDocumentsAsync(project, cancellationToken); // US-136 R1
            return project;
        }

        // US-024 R1, R3: edits the details. The name stays unique; every change is logged with old and new value.
        // Nothing is saved or logged when nothing changed.
        public async Task UpdateAsync(
            Guid projectId,
            string name,
            string? description,
            string intendedPurpose,
            string? scope,
            CancellationToken cancellationToken = default,
            uint? expectedVersion = null)
        {
            var project = await ProjectAsync(projectId, cancellationToken);
            Concurrency.Ensure(project.Version, expectedVersion); // US-092

            var changes = project.Update(name, description, intendedPurpose, scope);
            if (changes.Count == 0)
            {
                return;
            }

            if (changes.Any(c => c.Field == "Name")
                && await _projects.NameExistsAsync(project.OrganizationId, project.Name, project.Id, cancellationToken))
            {
                // The change is not saved, because SaveChanges is never reached
                throw new DomainException("Project name already in use");
            }

            await _log.LogAsync(ActivityAction.ProjectUpdated, project.Id, string.Join("; ", changes), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-024 R2, R3, R4: moves the project to another phase; EndOfSupport is final
        // US-025 R1: releasing can set the support end date in the same step
        // US-074 R1: end of support takes the day the product was last placed on the market and an optional later retention date
        // US-135 R2: end of support waits for a second person; returns false when it waits
        public async Task<bool> ChangePhaseAsync(
            Guid projectId, LifecyclePhase phase, DateOnly? supportEndsOn = null, string? supportReason = null,
            CancellationToken cancellationToken = default, DateOnly? lastPlacedOnMarket = null, DateOnly? keepUntil = null)
        {
            var project = await ProjectAsync(projectId, cancellationToken);
            var today = _clock.Today();

            if (phase == LifecyclePhase.EndOfSupport && project.Phase != LifecyclePhase.EndOfSupport)
            {
                var placed = lastPlacedOnMarket ?? today;
                project.CheckEndSupport(placed, keepUntil, today);
                await EnsureNoOpenEventsAsync(project, cancellationToken);
                var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
                await _approvals.RequestAsync(
                    organization, user, project.Id, ApprovalAction.EndSupport, project.Id, project.Version.ToString(),
                    $"Set \"{project.Name}\" to end of support (last placed on the market {placed:dd-MM-yyyy}"
                    + (keepUntil is { } until ? $", kept until {until:dd-MM-yyyy})" : ")"),
                    new EndSupportPayload(placed, keepUntil), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return false;
            }

            await ApplyPhaseAsync(project, phase, supportEndsOn, supportReason, today, null, null, null, null, cancellationToken);
            return true;
        }

        // US-073 R1: every security event is closed before the product leaves the market
        private async Task EnsureNoOpenEventsAsync(Project project, CancellationToken cancellationToken)
        {
            if ((await _securityEvents.ListAsync(project.Id, cancellationToken)).Any(e => e.IsOpen))
            {
                throw new DomainException("Close every security event before end of support");
            }
        }

        // ---------------------------------------------------------- US-135: the approved end of support

        ApprovalAction IApprovalExecutor.Action => ApprovalAction.EndSupport;

        Permission IApprovalExecutor.Permission => Permission.EditProjects;

        async Task<string?> IApprovalExecutor.WhyStaleAsync(ApprovalRequest request, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(request.OrganizationId, request.ProjectId, cancellationToken);
            return project is null ? "the project is gone"
                : project.IsClosed ? "the project is at end of support already"
                : project.Version.ToString() != request.SubjectStamp ? "the project changed after the request was made"
                : null;
        }

        async Task IApprovalExecutor.ExecuteAsync(ApprovalRequest request, Organization organization, User requester, User approver, CancellationToken cancellationToken)
        {
            var payload = ApprovalGate.Read<EndSupportPayload>(request);
            var project = await _projects.GetAsync(organization.Id, request.ProjectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            await EnsureNoOpenEventsAsync(project, cancellationToken);
            await ApplyPhaseAsync(project, LifecyclePhase.EndOfSupport, null, null, _clock.Today(),
                payload.LastPlacedOnMarket, payload.KeepUntil, requester, approver, cancellationToken, save: false);
        }

        private async Task ApplyPhaseAsync(
            Project project, LifecyclePhase phase, DateOnly? supportEndsOn, string? supportReason, DateOnly today,
            DateOnly? lastPlacedOnMarket, DateOnly? keepUntil, User? asUser, User? approver, CancellationToken cancellationToken,
            bool save = true)
        {
            if (phase == LifecyclePhase.Released && supportEndsOn is { } endsOn && project.Phase != LifecyclePhase.Released)
            {
                if (project.SetSupportPeriod(endsOn, supportReason, today) is { } support)
                {
                    await _log.LogAsync(ActivityAction.SupportPeriodSet, project.Id, Describe(support, project), cancellationToken);
                }
            }

            var change = phase == LifecyclePhase.EndOfSupport && project.Phase != LifecyclePhase.EndOfSupport
                ? project.EndSupport(lastPlacedOnMarket ?? today, keepUntil, today)
                : project.ChangePhase(phase, today);
            if (change is not null)
            {
                if (asUser is null)
                {
                    await _log.LogAsync(ActivityAction.ProjectPhaseChanged, project.Id, change.ToString(), cancellationToken);
                }
                else
                {
                    await _log.LogAsAsync(project.OrganizationId, asUser, ActivityAction.ProjectPhaseChanged, project.Id,
                        $"{change} (approved by {approver?.Name})", cancellationToken);
                }
            }

            if (save)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // US-104 R2, R4: the methods the project uses to describe its requirements (EditProjects), logged with old and new
        public async Task ChangeRequirementMethodsAsync(Guid projectId, RequirementMethods methods, CancellationToken cancellationToken = default)
        {
            var project = await ProjectAsync(projectId, cancellationToken);

            var change = project.ChangeRequirementMethods(methods);
            if (change is null)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.ProjectMethodsChanged, project.Id, change.ToString(), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-130 R4: leave a confidential project out of AI improvement (EditProjects), logged
        public async Task SetExcludedFromAiSharingAsync(Guid projectId, bool excluded, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");

            var change = project.SetExcludedFromAiSharing(excluded, user.Id, Now);
            if (change is null)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.ProjectAiSharingChanged, project.Id, change.ToString(), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-025 R2, R3, R5: set or move the support end date, logged with old and new
        public async Task SetSupportPeriodAsync(Guid projectId, DateOnly endsOn, string? reason, CancellationToken cancellationToken = default)
        {
            var project = await ProjectAsync(projectId, cancellationToken);
            var change = project.SetSupportPeriod(endsOn, reason, _clock.Today());
            if (change is null)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.SupportPeriodSet, project.Id, Describe(change, project), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static string Describe(ProjectChange change, Project project)
            => change + (project.SupportPeriodReason is { } reason ? $" ({reason})" : "");

        // US-011 R2, R3: adds a framework to an existing project.
        // Without a version, the newest one is used.
        public async Task AddFrameworkAsync(
            Guid projectId, Guid frameworkId, Guid? frameworkVersionId = null,
            CancellationToken cancellationToken = default)
        {
            var (userId, project, framework) = await LoadAsync(projectId, frameworkId, cancellationToken);

            var version = frameworkVersionId is null
                ? framework.RequireCurrentVersion(_clock.Today())
                : framework.Version(frameworkVersionId.Value);

            project.UseFramework(framework, version, userId, Now);
            await _log.LogAsync(ActivityAction.ProjectFrameworkAdded, project.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SyncDocumentsAsync(project, cancellationToken); // US-136 R1
        }

        // US-136 R1: the organisation's documents become evidence for the requirements the project now follows
        private async Task SyncDocumentsAsync(Project project, CancellationToken cancellationToken)
        {
            await _documents.SyncAsync(project.OrganizationId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-011 R6: moves the project to the newest version of a framework it uses (US-007: the current active one).
        // US-035 R5: with a version, the project moves to exactly that one, e.g. an upcoming version to prepare for it.
        public async Task UseNewestVersionAsync(
            Guid projectId, Guid frameworkId, Guid? frameworkVersionId = null, CancellationToken cancellationToken = default)
        {
            var (userId, project, framework) = await LoadAsync(projectId, frameworkId, cancellationToken);
            var version = frameworkVersionId is null
                ? framework.RequireCurrentVersion(_clock.Today())
                : framework.Version(frameworkVersionId.Value);

            var from = project.Frameworks.FirstOrDefault(f => f.FrameworkId == framework.Id)?.FrameworkVersionId;
            if (project.ChangeFrameworkVersion(framework, version, userId, Now))
            {
                // US-069 R6: links, "not applicable" and evidence follow to the requirements with the same code
                var carried = await _carryOver.CarryAsync(project, framework.Id, from!.Value, version.Id, cancellationToken);
                await _log.LogAsync(ActivityAction.ProjectFrameworkVersionChanged, project.Id, carried.Describe(framework.Name, version.Number), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await SyncDocumentsAsync(project, cancellationToken); // US-136 R1
            }
        }

        // US-011 R7: removes a framework from a project; the history is kept
        public async Task RemoveFrameworkAsync(Guid projectId, Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (userId, project, framework) = await LoadAsync(projectId, frameworkId, cancellationToken);

            project.RemoveFramework(framework, userId, Now);
            await _log.LogAsync(ActivityAction.ProjectFrameworkRemoved, project.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The user's own project; other organisations' projects are "not found"
        private async Task<Project> ProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            return await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
        }

        // The user, the project and the framework; all must belong to (or be followed by) the user's organisation
        private async Task<(Guid UserId, Project Project, Framework Framework)> LoadAsync(
            Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            var framework = await AvailableFrameworkAsync(organization.Id, frameworkId, cancellationToken);
            return (user.Id, project, framework);
        }

        // R1: an uploaded framework of the organisation, or a built-in one it has switched on
        private async Task<Framework> AvailableFrameworkAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken)
            => await _frameworks.GetAvailableAsync(organizationId, frameworkId, cancellationToken)
               ?? throw new DomainException("Framework not found");

        // The current time in UTC, from the clock (US-016)
        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
