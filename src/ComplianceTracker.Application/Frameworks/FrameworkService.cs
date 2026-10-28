using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Frameworks
{
    // US-005: tilføj frameworks og upload nye versioner som PDF
    public class FrameworkService
    {
        private readonly IFrameworkRepository _frameworks;
        private readonly IOrganizationRepository _organizations;
        private readonly IActivityLogRepository _activityLog;
        private readonly IUnitOfWork _unitOfWork;

        public FrameworkService(
            IFrameworkRepository frameworks,
            IOrganizationRepository organizations,
            IActivityLogRepository activityLog,
            IUnitOfWork unitOfWork)
        {
            _frameworks = frameworks;
            _organizations = organizations;
            _activityLog = activityLog;
            _unitOfWork = unitOfWork;
        }

        public async Task<Framework> AddAsync(string userExternalId, string name, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, userId) = await MembershipAsync(userExternalId, cancellationToken);
            var file = CatalogFile.Create(fileName, content); // R5 + R6

            if (await _frameworks.NameExistsAsync(organization.Id, name.Trim(), cancellationToken))
            {
                throw new DomainException("Framework already exists. Upload a new version instead"); // R4
            }

            var framework = Framework.Create(organization.Id, name, file, userId, DateTime.UtcNow);
            await _frameworks.AddAsync(framework, cancellationToken);
            await SaveVersionAsync(organization.Id, userId, framework.CurrentVersion, file, cancellationToken);
            return framework;
        }

        public async Task<FrameworkVersion> UploadVersionAsync(string userExternalId, Guid frameworkId, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, userId) = await MembershipAsync(userExternalId, cancellationToken);
            var file = CatalogFile.Create(fileName, content);

            var framework = await _frameworks.GetAsync(organization.Id, frameworkId, cancellationToken)
                ?? throw new DomainException("Framework not found"); // R7: også når det tilhører en anden

            var version = framework.AddVersion(file, userId, DateTime.UtcNow); // R3
            await SaveVersionAsync(organization.Id, userId, version, file, cancellationToken);
            return version;
        }

        // Gem PDF'en og logposten sammen med versionen i én transaktion (R8)
        private async Task SaveVersionAsync(Guid organizationId, Guid userId, FrameworkVersion version, CatalogFile file, CancellationToken cancellationToken)
        {
            await _frameworks.AddDocumentAsync(version.Id, file.Content, cancellationToken);
            await _activityLog.AppendAsync(
                new ActivityLogEntry(organizationId, userId, ActivityAction.CatalogImported, DateTime.UtcNow, version.Id),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<(Organization Organization, Guid UserId)> MembershipAsync(string userExternalId, CancellationToken cancellationToken)
        {
            var organization = await _organizations.GetByMemberAsync(userExternalId, cancellationToken)
                ?? throw new DomainException("You must belong to an organisation");
            var user = organization.Users.Single(u => u.ExternalId == userExternalId);
            return (organization, user.Id);
        }
    }
}