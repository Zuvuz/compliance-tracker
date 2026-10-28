using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // Storage for projects (implemented in Infrastructure).
    // Every lookup takes the organisation's id, so a user can never reach another
    // organisation's projects, not even by guessing an id (R5, ASVS V8.2.2).
    // AddAsync only registers the project; IUnitOfWork saves it.
    public interface IProjectRepository
    {
        // True if the organisation already has a project with this name (upper/lower case ignored)
        Task<bool> NameExistsAsync(Guid organizationId, string name, CancellationToken cancellationToken = default);

        Task AddAsync(Project project, CancellationToken cancellationToken = default);

        // Only the organisation's own projects. Others return null.
        Task<Project?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken = default);

        // The organisation's projects in alphabetical order
        Task<IReadOnlyList<Project>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);
    }
}
