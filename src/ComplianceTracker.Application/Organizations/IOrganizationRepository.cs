using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Organizations
{
    // Storage for organisations and their members (implemented in Infrastructure, and in memory in the tests).
    // Repositories do not save on their own; that happens when IUnitOfWork.SaveChangesAsync is called.
    public interface IOrganizationRepository
    {
        Task<bool> CvrExistsAsync(string cvr, CancellationToken cancellationToken = default);
        Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
        Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        // The organisation the user with this sign-in id is a member of (null if none)
        Task<Organization?> GetByMemberAsync(string externalId, CancellationToken cancellationToken = default);
    }
}
