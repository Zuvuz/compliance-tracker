using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // Use cases for projects: create a project (US-008) and choose its frameworks (US-011).
    // The domain checks the fields and the rules about one project; this class checks the rules
    // that need the database and saves the changes and their log entries together.
    public class ProjectService
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IOrganizationRepository _organizations;
        private readonly IActivityLogRepository _activityLog;
        private readonly IUnitOfWork _unitOfWork;

        // The clock. TimeProvider.System in the app; a fixed time in tests (US-016 R2)
        private readonly TimeProvider _clock;

        public ProjectService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IOrganizationRepository organizations,
            IActivityLogRepository activityLog,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _organizations = organizations;
            _activityLog = activityLog;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // US-008: creates a project. US-011 R2: the newest version of each chosen framework is added at once.
        public async Task<Project> CreateAsync(
            string userExternalId,
            string name,
            string? description,
            string intendedPurpose,
            string? scope,
            LifecyclePhase phase,
            IReadOnlyCollection<Guid>? frameworkIds = null,
            CancellationToken cancellationToken = default)
        {
            var (organizationId, userId) = await MembershipAsync(userExternalId, cancellationToken);

            // 1. Validate the fields (R2)
            var project = Project.Create(organizationId, name, description, intendedPurpose, scope, phase);

            // 2. R3: the name is unique within the organisation
            if (await _projects.NameExistsAsync(organizationId, project.Name, cancellationToken))
            {
                throw new DomainException("Project name already in use");
            }

            // 3. Register the project and the log entry (R6)
            var now = Now;
            await _projects.AddAsync(project, cancellationToken);
            await LogAsync(organizationId, userId, ActivityAction.ProjectCreated, project, cancellationToken);

            // 4. US-011 R1, R2: the chosen frameworks, each logged
            foreach (var frameworkId in (frameworkIds ?? []).Distinct())
            {
                var framework = await AvailableFrameworkAsync(organizationId, frameworkId, cancellationToken);
                project.UseFramework(framework, framework.CurrentVersion, userId, now);
                await LogAsync(organizationId, userId, ActivityAction.ProjectFrameworkAdded, project, cancellationToken);
            }

            // 5. Save it all at once
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return project;
        }

        // US-011 R2, R3: adds a framework to an existing project.
        // Without a version, the newest one is used.
        public async Task AddFrameworkAsync(
            string userExternalId, Guid projectId, Guid frameworkId, Guid? frameworkVersionId = null,
            CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project, framework) = await LoadAsync(userExternalId, projectId, frameworkId, cancellationToken);

            var version = frameworkVersionId is null
                ? framework.CurrentVersion
                : framework.Versions.FirstOrDefault(v => v.Id == frameworkVersionId)
                  ?? throw new DomainException("Framework version not found");

            project.UseFramework(framework, version, userId, Now);
            await LogAsync(organizationId, userId, ActivityAction.ProjectFrameworkAdded, project, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-011 R6: moves the project to the newest version of a framework it uses
        public async Task UseNewestVersionAsync(
            string userExternalId, Guid projectId, Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project, framework) = await LoadAsync(userExternalId, projectId, frameworkId, cancellationToken);

            if (project.ChangeFrameworkVersion(framework, framework.CurrentVersion, userId, Now))
            {
                await LogAsync(organizationId, userId, ActivityAction.ProjectFrameworkVersionChanged, project, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // US-011 R7: removes a framework from a project; the history is kept
        public async Task RemoveFrameworkAsync(
            string userExternalId, Guid projectId, Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organizationId, userId, project, framework) = await LoadAsync(userExternalId, projectId, frameworkId, cancellationToken);

            project.RemoveFramework(framework, userId, Now);
            await LogAsync(organizationId, userId, ActivityAction.ProjectFrameworkRemoved, project, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The user's organisation, the project and the framework; all must belong to (or be followed by) the organisation
        private async Task<(Guid OrganizationId, Guid UserId, Project Project, Framework Framework)> LoadAsync(
            string userExternalId, Guid projectId, Guid frameworkId, CancellationToken cancellationToken)
        {
            var (organizationId, userId) = await MembershipAsync(userExternalId, cancellationToken);
            var project = await _projects.GetAsync(organizationId, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            var framework = await AvailableFrameworkAsync(organizationId, frameworkId, cancellationToken);
            return (organizationId, userId, project, framework);
        }

        // R1: an uploaded framework of the organisation, or a built-in one it has switched on
        private async Task<Framework> AvailableFrameworkAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken)
            => await _frameworks.GetAvailableAsync(organizationId, frameworkId, cancellationToken)
               ?? throw new DomainException("Framework not found");

        // R9: who did what to which project, and when. The ProjectFramework rows hold the framework and versions.
        private Task LogAsync(Guid organizationId, Guid userId, ActivityAction action, Project project, CancellationToken cancellationToken)
            => _activityLog.AppendAsync(
                new ActivityLogEntry(organizationId, userId, action, Now, project.Id),
                cancellationToken);

        // The signed-in user's organisation, and the user's id in it
        private async Task<(Guid OrganizationId, Guid UserId)> MembershipAsync(string userExternalId, CancellationToken cancellationToken)
        {
            var organization = await _organizations.GetByMemberAsync(userExternalId, cancellationToken)
                ?? throw new DomainException("You must belong to an organisation");
            var user = organization.Users.Single(u => u.ExternalId == userExternalId);
            return (organization.Id, user.Id);
        }

        // The current time in UTC, from the clock
        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
