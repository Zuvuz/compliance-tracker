using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Specs.Support
{
    public class InMemoryOrganizationRepository : IOrganizationRepository
    {
        private readonly List<Organization> _items = new();

        public bool CvrExists(string cvr) => _items.Any(o => o.Cvr == cvr);

        public void Add(Organization organization) => _items.Add(organization);
    }
}