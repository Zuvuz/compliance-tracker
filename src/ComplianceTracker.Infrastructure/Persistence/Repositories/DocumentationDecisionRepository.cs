using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IDocumentationDecisionRepository (US-204). Always for one organisation.
    public class DocumentationDecisionRepository : IDocumentationDecisionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public DocumentationDecisionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // One query; the newest per requirement is picked in memory (the decisions of an organisation are few)
        public async Task<IReadOnlyList<DocumentationDecision>> ListNewestAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => (await _db.DocumentationDecisions
                    .Where(d => d.OrganizationId == organizationId)
                    .ToListAsync(cancellationToken))
                .GroupBy(d => d.RequirementId)
                .Select(g => g.OrderByDescending(d => d.DecidedAt).First())
                .ToList();

        public async Task AddAsync(DocumentationDecision decision, CancellationToken cancellationToken = default)
            => await _db.DocumentationDecisions.AddAsync(decision, cancellationToken);
    }
}
