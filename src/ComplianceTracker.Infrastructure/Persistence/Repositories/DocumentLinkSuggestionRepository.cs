using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-136 R2
    public class DocumentLinkSuggestionRepository : IDocumentLinkSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public DocumentLinkSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<DocumentLinkSuggestion>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.DocumentLinkSuggestions
                .Where(s => s.OrganizationId == organizationId)
                .OrderBy(s => s.Rank)
                .ToListAsync(cancellationToken);

        public Task AddRangeAsync(IEnumerable<DocumentLinkSuggestion> suggestions, CancellationToken cancellationToken = default)
            => _db.DocumentLinkSuggestions.AddRangeAsync(suggestions, cancellationToken);
    }
}
