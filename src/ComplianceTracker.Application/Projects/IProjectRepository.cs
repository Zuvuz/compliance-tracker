using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // Storage for projects (implemented in Infrastructure).
    // Every lookup takes the organisation's id, so a user can never reach another
    // organisation's projects, not even by guessing an id (R5, ASVS V8.2.2).
    // AddAsync only registers the project; IUnitOfWork saves it.
    // A project is always loaded with its frameworks (US-011).
    public interface IProjectRepository
    {
        // True if the organisation already has a project with this name (upper/lower case ignored)
        // exceptProjectId: the project being renamed, which may keep its own name (US-024 R1)
        Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? exceptProjectId = null, CancellationToken cancellationToken = default);

        Task AddAsync(Project project, CancellationToken cancellationToken = default);

        // Only the organisation's own projects. Others return null.
        Task<Project?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);

        // The organisation's projects in alphabetical order
        Task<IReadOnlyList<Project>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // The organisation's projects that use any version of this framework now (US-011 R4, R8)
        Task<IReadOnlyList<Project>> ListUsingFrameworkAsync(Guid organizationId, Guid frameworkId, CancellationToken cancellationToken = default);

        // ---------------------------------------------------------- Only for the scheduled GitHub check (US-018 R6)
        // These two ignore the organisation, because the check runs for every organisation without a user.
        // Nothing they return is ever shown to a user.

        Task<IReadOnlyList<Guid>> ListIdsLinkedToGitHubAsync(CancellationToken cancellationToken = default);

        Task<Project?> GetForSyncAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
