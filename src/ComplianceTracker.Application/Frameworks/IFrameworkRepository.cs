using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    public interface IFrameworkRepository
    {
        Task<bool> NameExistsAsync(Guid organizationId, string name, CancellationToken cancellationToken = default);
        Task AddAsync(Framework framework, CancellationToken cancellationToken = default);
        Task AddDocumentAsync(Guid frameworkVersionId, byte[] content, CancellationToken cancellationToken = default);

        // Kun organisationens egne frameworks (R7). Andre giver null.
        Task<Framework?> GetAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Framework>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // PDF'en for en version, men kun hvis den hører til organisationen (R7)
        Task<(FrameworkVersion Version, byte[] Content)?> GetDocumentAsync(Guid organizationId, Guid frameworkVersionId, CancellationToken cancellationToken = default);
    }
}