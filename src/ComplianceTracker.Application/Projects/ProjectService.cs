using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // Use cases that change projects: create a project (US-008), choose its frameworks (US-011),
    // and edit it or change its lifecycle phase (US-024).
    // The domain checks the fields and the rules about one project; this class checks the rules
    // that need the database and saves the changes and their log entries together.
    // Always acts for the signed-in member (CurrentMember); reading is done by ProjectQueries.
    public class ProjectService
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ProjectService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
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
            var (organization, user) = await _member.RequireAsync(cancellationToken);

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
            CancellationToken cancellationToken = default)
        {
            var project = await ProjectAsync(projectId, cancellationToken);

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
        public async Task ChangePhaseAsync(Guid projectId, LifecyclePhase phase, CancellationToken cancellationToken = default)
        {
            var project = await ProjectAsync(projectId, cancellationToken);

            var change = project.ChangePhase(phase);
            if (change is null)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.ProjectPhaseChanged, project.Id, change.ToString(), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

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

            if (project.ChangeFrameworkVersion(framework, version, userId, Now))
            {
                await _log.LogAsync(ActivityAction.ProjectFrameworkVersionChanged, project.Id, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            return await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
        }

        // The user, the project and the framework; all must belong to (or be followed by) the user's organisation
        private async Task<(Guid UserId, Project Project, Framework Framework)> LoadAsync(
            Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
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
