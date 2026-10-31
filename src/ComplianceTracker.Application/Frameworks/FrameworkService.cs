using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // Use cases that change frameworks: add and upload versions (US-005), switch built-in ones on and off (US-006),
    // and check, correct and activate the requirements of a new version (US-007).
    // Holds the rules that need the database (unique names, membership) and saves everything at once.
    // Always acts for the signed-in member (CurrentMember); reading is done by FrameworkQueries.
    public class FrameworkService
    {
        private readonly IFrameworkRepository _frameworks;
        private readonly IProjectRepository _projects;
        private readonly IRequirementExtractor _extractor;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public FrameworkService(
            IFrameworkRepository frameworks,
            IProjectRepository projects,
            IRequirementExtractor extractor,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _frameworks = frameworks;
            _projects = projects;
            _extractor = extractor;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // Adds a new framework to the organisation, with the PDF as version 1.
        // US-007 R1: the version is a draft with the requirements found in the PDF.
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
            AddFoundRequirements(framework.LatestVersion, file);
            await _frameworks.AddAsync(framework, cancellationToken);
            await SaveVersionAsync(framework.LatestVersion, file, cancellationToken);
            return framework;
        }

        // Uploads a new version of an existing framework; earlier versions are kept (R3).
        // US-007: the new version is a draft, so every project keeps its version until it is activated
        // and the project is moved with "Use newest" (US-011 R6).
        public async Task<FrameworkVersion> UploadVersionAsync(
            Guid frameworkId, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var file = CatalogFile.Create(fileName, content);

            // R7: the lookup only finds the organisation's own frameworks,
            // so a framework owned by someone else looks exactly like one that does not exist
            var framework = await _frameworks.GetAsync(organization.Id, frameworkId, cancellationToken)
                ?? throw new DomainException("Framework not found");

            var version = framework.AddVersion(file, user.Id, Now);
            AddFoundRequirements(version, file);
            await SaveVersionAsync(version, file, cancellationToken);
            return version;
        }

        // ---------------------------------------------------------- Draft requirements (US-007)

        // R2, R5: adds a requirement by hand
        public async Task AddRequirementAsync(Guid frameworkVersionId, string code, string title, string? description, CancellationToken cancellationToken = default)
        {
            var version = await DraftAsync(frameworkVersionId, cancellationToken);
            version.AddRequirement(code, title, description);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: corrects one requirement
        public async Task ChangeRequirementAsync(Guid frameworkVersionId, Guid requirementId, string code, string title, string? description, CancellationToken cancellationToken = default)
        {
            var version = await DraftAsync(frameworkVersionId, cancellationToken);
            version.ChangeRequirement(requirementId, code, title, description);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: removes a wrong row
        public async Task RemoveRequirementAsync(Guid frameworkVersionId, Guid requirementId, CancellationToken cancellationToken = default)
        {
            var version = await DraftAsync(frameworkVersionId, cancellationToken);
            version.RemoveRequirement(requirementId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R3, R4, R6: locks the version; it becomes the framework's current version. Logged.
        public async Task ActivateAsync(Guid frameworkVersionId, CancellationToken cancellationToken = default)
        {
            var (_, user) = await _member.RequireAsync(cancellationToken);
            var version = await DraftAsync(frameworkVersionId, cancellationToken);

            version.Activate(user.Id, Now);
            await _log.LogAsync(ActivityAction.CatalogActivated, version.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // One of the organisation's own versions, with its requirements loaded.
        // Whether it is still a draft is checked by the domain ("An active version cannot be changed").
        private async Task<FrameworkVersion> DraftAsync(Guid frameworkVersionId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var framework = await _frameworks.GetByVersionWithRequirementsAsync(organization.Id, frameworkVersionId, cancellationToken)
                ?? throw new DomainException("Framework version not found");
            return framework.Version(frameworkVersionId);
        }

        // R1: the requirements found in the PDF become the draft's rows
        private void AddFoundRequirements(FrameworkVersion version, CatalogFile file)
        {
            foreach (var found in _extractor.Extract(file.Content))
            {
                version.AddRequirement(found.Code, found.Title, found.Description);
            }
        }

        // US-006: switches a built-in framework on for the organisation (R2, R3).
        // Switching on a framework that is already on does nothing.
        public async Task EnableBuiltInAsync(Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var version = (await BuiltInAsync(frameworkId, cancellationToken)).RequireCurrentVersion();

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
            var version = framework.RequireCurrentVersion();

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
