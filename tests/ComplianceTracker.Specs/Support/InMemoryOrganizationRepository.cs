using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Specs.Support
{
    // A fake repository kept in a list, for the fast US-001 tests that do not use a database.
    // It behaves like the real one, so OrganizationService cannot tell the difference.
    public class InMemoryOrganizationRepository : IOrganizationRepository
    {
        private readonly List<Organization> _items = new();

        public Task<bool> CvrExistsAsync(string cvr, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(o => o.Cvr == cvr));

        public Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            _items.Add(organization);
            return Task.CompletedTask;
        }

        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(o => o.Id == id));

        public Task<Organization?> GetByMemberAsync(string externalId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(o => o.Users.Any(u => u.ExternalId == externalId)));
    }
}
