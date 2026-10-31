using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Frameworks
{
    // US-005 use cases: add a framework with its first PDF, and upload new versions.
    // Holds the rules that need the database (unique names, membership) and saves everything at once.
    public class FrameworkService
    {
        private readonly IFrameworkRepository _frameworks;
        private readonly IProjectRepository _projects;
        private readonly IOrganizationRepository _organizations;
        private readonly IActivityLogRepository _activityLog;
        private readonly IUnitOfWork _unitOfWork;

        // The clock. TimeProvider.System in the app; a fixed time in tests (US-016 R2)
        private readonly TimeProvider _clock;

        public FrameworkService(
            IFrameworkRepository frameworks,
            IProjectRepository projects,
            IOrganizationRepository organizations,
            IActivityLogRepository activityLog,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _frameworks = frameworks;
            _projects = projects;
            _organizations = organizations;
            _activityLog = activityLog;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // Adds a new framework to the signed-in user's organisation, with the PDF as version 1
        public async Task<Framework> AddAsync(string userExternalId, string name, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, userId) = await MembershipAsync(userExternalId, cancellationToken);
            var file = CatalogFile.Create(fileName, content); // R5 + R6: valid PDF, safe file name

            // R4: a name can only be used once per organisation (the repository ignores upper/lower case)
            if (await _frameworks.NameExistsAsync(organization.Id, name.Trim(), cancellationToken))
            {
                throw new DomainException("Framework already exists. Upload a new version instead");
            }

            var framework = Framework.Create(organization.Id, name, file, userId, Now);
            await _frameworks.AddAsync(framework, cancellationToken);
            await SaveVersionAsync(organization.Id, userId, framework.CurrentVersion, file, cancellationToken);
            return framework;
        }

        // Uploads a new version of an existing framework; earlier versions are kept (R3).
        // US-011 R4: the ticked projects move to the new version; all other projects keep theirs.
        public async Task<FrameworkVersion> UploadVersionAsync(
            string userExternalId, Guid frameworkId, string fileName, byte[] content,
            IReadOnlyCollection<Guid>? moveProjectIds = null, CancellationToken cancellationToken = default)
        {
            var (organization, userId) = await MembershipAsync(userExternalId, cancellationToken);
            var file = CatalogFile.Create(fileName, content);

            // R7: the lookup only finds the organisation's own frameworks,
            // so a framework owned by someone else looks exactly like one that does not exist
            var framework = await _frameworks.GetAsync(organization.Id, frameworkId, cancellationToken)
                ?? throw new DomainException("Framework not found");

            var now = Now;
            var version = framework.AddVersion(file, userId, now);

            // US-011 R4: only projects that already use the framework can move; saved together with the version
            foreach (var projectId in (moveProjectIds ?? []).Distinct())
            {
                var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                    ?? throw new DomainException("Project not found");
                if (project.ChangeFrameworkVersion(framework, version, userId, now))
                {
                    await _activityLog.AppendAsync(
                        new ActivityLogEntry(organization.Id, userId, ActivityAction.ProjectFrameworkVersionChanged, now, project.Id),
                        cancellationToken);
                }
            }

            await SaveVersionAsync(organization.Id, userId, version, file, cancellationToken);
            return version;
        }

        // US-006: switches a built-in framework on for the user's organisation (R2, R3).
        // Switching on a framework that is already on does nothing.
        public async Task EnableBuiltInAsync(string userExternalId, Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organization, userId) = await MembershipAsync(userExternalId, cancellationToken);
            var framework = await _frameworks.GetBuiltInAsync(frameworkId, cancellationToken)
                ?? throw new DomainException("Framework not found");
            var version = framework.CurrentVersion;

            if (await _frameworks.GetActiveSelectionAsync(organization.Id, version.Id, cancellationToken) is not null)
            {
                return;
            }

            await _frameworks.AddSelectionAsync(
                OrganizationFramework.Enable(organization.Id, version.Id, userId, Now),
                cancellationToken);
            await _activityLog.AppendAsync(
                new ActivityLogEntry(organization.Id, userId, ActivityAction.FrameworkEnabled, Now, version.Id),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-006: switches a built-in framework off again. The history is kept (R4).
        // Switching off a framework that is already off does nothing.
        public async Task DisableBuiltInAsync(string userExternalId, Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organization, userId) = await MembershipAsync(userExternalId, cancellationToken);
            var framework = await _frameworks.GetBuiltInAsync(frameworkId, cancellationToken)
                ?? throw new DomainException("Framework not found");
            var version = framework.CurrentVersion;

            var selection = await _frameworks.GetActiveSelectionAsync(organization.Id, version.Id, cancellationToken);
            if (selection is null)
            {
                return;
            }

            // US-011 R8: not while projects use it, so no project is left following a framework that is off
            var users = await _projects.ListUsingFrameworkAsync(organization.Id, framework.Id, cancellationToken);
            if (users.Count > 0)
            {
                throw new DomainException("Used by projects: " + string.Join(", ", users.Select(p => p.Name)));
            }

            selection.Disable(userId, Now);
            await _activityLog.AppendAsync(
                new ActivityLogEntry(organization.Id, userId, ActivityAction.FrameworkDisabled, Now, version.Id),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Saves the PDF and the log entry together with the version in one transaction (R8).
        // If anything fails, none of it is stored.
        private async Task SaveVersionAsync(Guid organizationId, Guid userId, FrameworkVersion version, CatalogFile file, CancellationToken cancellationToken)
        {
            await _frameworks.AddDocumentAsync(version.Id, file.Content, cancellationToken);
            await _activityLog.AppendAsync(
                new ActivityLogEntry(organizationId, userId, ActivityAction.CatalogImported, Now, version.Id),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Finds the organisation the signed-in user belongs to, and the user's id in it
        private async Task<(Organization Organization, Guid UserId)> MembershipAsync(string userExternalId, CancellationToken cancellationToken)
        {
            var organization = await _organizations.GetByMemberAsync(userExternalId, cancellationToken)
                ?? throw new DomainException("You must belong to an organisation");
            var user = organization.Users.Single(u => u.ExternalId == userExternalId);
            return (organization, user.Id);
        }

        // The current time in UTC, from the clock
        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
