using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // Use cases that change frameworks: add and upload versions (US-005), switch built-in ones on and off (US-006).
    // Holds the rules that need the database (unique names, membership) and saves everything at once.
    // Always acts for the signed-in member (CurrentMember); reading is done by FrameworkQueries.
    public class FrameworkService
    {
        private readonly IFrameworkRepository _frameworks;
        private readonly IProjectRepository _projects;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public FrameworkService(
            IFrameworkRepository frameworks,
            IProjectRepository projects,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _frameworks = frameworks;
            _projects = projects;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // Adds a new framework to the organisation, with the PDF as version 1
        public async Task<Framework> AddAsync(string name, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var file = CatalogFile.Create(fileName, content); // R5 + R6: valid PDF, safe file name

            // R4: a name can only be used once per organisation (the repository ignores upper/lower case)
            if (await _frameworks.NameExistsAsync(organization.Id, name.Trim(), cancellationToken))
            {
                throw new DomainException("Framework already exists. Upload a new version instead");
            }

            var framework = Framework.Create(organization.Id, name, file, user.Id, Now);
            await _frameworks.AddAsync(framework, cancellationToken);
            await SaveVersionAsync(framework.CurrentVersion, file, cancellationToken);
            return framework;
        }

        // Uploads a new version of an existing framework; earlier versions are kept (R3).
        // US-011 R4: the ticked projects move to the new version; all other projects keep theirs.
        public async Task<FrameworkVersion> UploadVersionAsync(
            Guid frameworkId, string fileName, byte[] content,
            IReadOnlyCollection<Guid>? moveProjectIds = null, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var file = CatalogFile.Create(fileName, content);

            // R7: the lookup only finds the organisation's own frameworks,
            // so a framework owned by someone else looks exactly like one that does not exist
            var framework = await _frameworks.GetAsync(organization.Id, frameworkId, cancellationToken)
                ?? throw new DomainException("Framework not found");

            var now = Now;
            var version = framework.AddVersion(file, user.Id, now);

            // US-011 R4: only projects that already use the framework can move; saved together with the version
            foreach (var projectId in (moveProjectIds ?? []).Distinct())
            {
                var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                    ?? throw new DomainException("Project not found");
                if (project.ChangeFrameworkVersion(framework, version, user.Id, now))
                {
                    await _log.LogAsync(ActivityAction.ProjectFrameworkVersionChanged, project.Id, cancellationToken);
                }
            }

            await SaveVersionAsync(version, file, cancellationToken);
            return version;
        }

        // US-006: switches a built-in framework on for the organisation (R2, R3).
        // Switching on a framework that is already on does nothing.
        public async Task EnableBuiltInAsync(Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var version = (await BuiltInAsync(frameworkId, cancellationToken)).CurrentVersion;

            if (await _frameworks.GetActiveSelectionAsync(organization.Id, version.Id, cancellationToken) is not null)
            {
                return;
            }

            await _frameworks.AddSelectionAsync(
                OrganizationFramework.Enable(organization.Id, version.Id, user.Id, Now),
                cancellationToken);
            await _log.LogAsync(ActivityAction.FrameworkEnabled, version.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-006: switches a built-in framework off again. The history is kept (R4).
        // Switching off a framework that is already off does nothing.
        public async Task DisableBuiltInAsync(Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var framework = await BuiltInAsync(frameworkId, cancellationToken);
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

            selection.Disable(user.Id, Now);
            await _log.LogAsync(ActivityAction.FrameworkDisabled, version.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<Framework> BuiltInAsync(Guid frameworkId, CancellationToken cancellationToken)
            => await _frameworks.GetBuiltInAsync(frameworkId, cancellationToken)
               ?? throw new DomainException("Framework not found");

        // Saves the PDF and the log entry together with the version in one transaction (R8).
        // If anything fails, none of it is stored.
        private async Task SaveVersionAsync(FrameworkVersion version, CatalogFile file, CancellationToken cancellationToken)
        {
            await _frameworks.AddDocumentAsync(version.Id, file.Content, cancellationToken);
            await _log.LogAsync(ActivityAction.CatalogImported, version.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The current time in UTC, from the clock (US-016)
        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
