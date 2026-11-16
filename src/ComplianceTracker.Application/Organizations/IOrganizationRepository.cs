using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Organizations
{
    // Storage for organisations and their members (implemented in Infrastructure, and in memory in the tests).
    // Repositories do not save on their own; that happens when IUnitOfWork.SaveChangesAsync is called.
    // US-187: one organisation the signed-in person is a member of
    public sealed record Membership(Guid OrganizationId, string Name, Domain.Users.UserRole Role)
    {
        // US-182: an auditor's access limited to one project and a period
        public Guid? OnlyProjectId { get; init; }
        public DateOnly? AccessFrom { get; init; }
        public DateOnly? AccessUntil { get; init; }

        public bool IsOpenOn(DateOnly day) => AccessFrom is not { } from || (day >= from && day <= AccessUntil);
    }

    public interface IOrganizationRepository
    {
        Task<bool> CvrExistsAsync(string cvr, CancellationToken cancellationToken = default);
        Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
        Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        // US-010: every organisation, for the system admin only
        Task<IReadOnlyList<Organization>> ListAllAsync(CancellationToken cancellationToken = default);

        // US-187: every organisation the person is an active member of, with their role there; their own organisation
        // (where their role is not external) first, then by name
        Task<IReadOnlyList<Membership>> ListMembershipsAsync(string externalId, CancellationToken cancellationToken = default);

        // US-090 R3: the name of the organisation the person was last removed from, or null
        Task<string?> FormerOrganizationNameAsync(string externalId, CancellationToken cancellationToken = default);
    }
}
