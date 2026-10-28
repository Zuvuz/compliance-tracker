using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // US-008 use case: create a project in the signed-in user's organisation.
    // The domain checks the fields; this class checks the rules that need the database
    // and saves the project and its log entry together.
    public class ProjectService
    {
        private readonly IProjectRepository _projects;
        private readonly IOrganizationRepository _organizations;
        private readonly IActivityLogRepository _activityLog;
        private readonly IUnitOfWork _unitOfWork;

        public ProjectService(
            IProjectRepository projects,
            IOrganizationRepository organizations,
            IActivityLogRepository activityLog,
            IUnitOfWork unitOfWork)
        {
            _projects = projects;
            _organizations = organizations;
            _activityLog = activityLog;
            _unitOfWork = unitOfWork;
        }

        public async Task<Project> CreateAsync(
            string userExternalId,
            string name,
            string? description,
            string intendedPurpose,
            string? scope,
            LifecyclePhase phase,
            CancellationToken cancellationToken = default)
        {
            // The signed-in user's organisation, and the user's id in it
            var organization = await _organizations.GetByMemberAsync(userExternalId, cancellationToken)
                ?? throw new DomainException("You must belong to an organisation");
            var user = organization.Users.Single(u => u.ExternalId == userExternalId);

            // 1. Validate the fields (R2)
            var project = Project.Create(organization.Id, name, description, intendedPurpose, scope, phase);

            // 2. R3: the name is unique within the organisation
            if (await _projects.NameExistsAsync(organization.Id, project.Name, cancellationToken))
            {
                throw new DomainException("Project name already in use");
            }

            // 3. Register the project and the log entry (R6), and save both at once
            await _projects.AddAsync(project, cancellationToken);
            await _activityLog.AppendAsync(
                new ActivityLogEntry(organization.Id, user.Id, ActivityAction.ProjectCreated, DateTime.UtcNow, project.Id),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return project;
        }
    }
}
