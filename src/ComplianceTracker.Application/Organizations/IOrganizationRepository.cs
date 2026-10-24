using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Organizations
{
    // Repositories gemmer ikke selv. Det sker først, når IUnitOfWork.SaveChangesAsync kaldes.
    public interface IOrganizationRepository
    {
        Task<bool> CvrExistsAsync(string cvr, CancellationToken cancellationToken = default);
        Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
        Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
