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

        // Only the organisation's own (uploaded) frameworks. Others, and built-in ones, return null.
        Task<Framework?> GetAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default);

        // US-007: like GetAsync, but with every version's requirements loaded.
        // Found by the id of one of its versions; only the organisation's own frameworks.
        Task<Framework?> GetByVersionWithRequirementsAsync(Guid organizationId, Guid frameworkVersionId, CancellationToken cancellationToken = default);

        // US-019, US-021: these frameworks (if the organisation follows them), with every version's requirements
        Task<IReadOnlyList<Framework>> ListAvailableWithRequirementsAsync(Guid organizationId, IReadOnlyCollection<Guid> frameworkIds, CancellationToken cancellationToken = default);

        // The frameworks the organisation follows: its own uploaded ones and the built-in ones it has switched on (US-006 R3)
        Task<IReadOnlyList<Framework>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // One framework the organisation follows (as in ListAsync), or null. Used when projects choose frameworks (US-011 R1).
        Task<Framework?> GetAvailableAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default);

        // ---------------------------------------------------------- Built-in frameworks (US-006)

        // Every built-in framework, whether switched on or not
        Task<IReadOnlyList<Framework>> ListBuiltInAsync(CancellationToken cancellationToken = default);

        // A built-in framework by id. Uploaded frameworks return null.
        Task<Framework?> GetBuiltInAsync(Guid frameworkId, CancellationToken cancellationToken = default);

        // The organisation's current "switched on" record for a framework version, or null if it is off
        Task<OrganizationFramework?> GetActiveSelectionAsync(Guid organizationId, Guid frameworkVersionId, CancellationToken cancellationToken = default);

        Task AddSelectionAsync(OrganizationFramework selection, CancellationToken cancellationToken = default);

        // The PDF for a version, but only if it belongs to the organisation. Others return null.
        Task<(FrameworkVersion Version, byte[] Content)?> GetDocumentAsync(Guid organizationId, Guid frameworkVersionId, CancellationToken cancellationToken = default);
    }
}
