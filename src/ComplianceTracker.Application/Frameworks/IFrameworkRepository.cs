using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Application.Frameworks
{
    // Storage for frameworks, their versions and the PDFs (implemented in Infrastructure).
    // Every lookup takes the organisation's id, so a user can never reach another
    // organisation's frameworks, not even by guessing an id (R7, ASVS V8.2.2).
    // Add methods only register changes; IUnitOfWork saves them.
    public interface IFrameworkRepository
    {
        // True if the organisation already has a framework with this name (upper/lower case ignored)
        Task<bool> NameExistsAsync(Guid organizationId, string name, CancellationToken cancellationToken = default);

        Task AddAsync(Framework framework, CancellationToken cancellationToken = default);

        // Stores the PDF content for a version (kept apart from the version itself)
        Task AddDocumentAsync(Guid frameworkVersionId, byte[] content, CancellationToken cancellationToken = default);

        // Only the organisation's own frameworks. Others return null.
        Task<Framework?> GetAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Framework>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // The PDF for a version, but only if it belongs to the organisation. Others return null.
        Task<(FrameworkVersion Version, byte[] Content)?> GetDocumentAsync(Guid organizationId, Guid frameworkVersionId, CancellationToken cancellationToken = default);
    }
}
