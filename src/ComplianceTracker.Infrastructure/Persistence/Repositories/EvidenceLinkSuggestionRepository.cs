using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IEvidenceLinkSuggestionRepository (US-059). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class EvidenceLinkSuggestionRepository : IEvidenceLinkSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public EvidenceLinkSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<EvidenceLinkSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.EvidenceLinkSuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task AddRangeAsync(IEnumerable<EvidenceLinkSuggestion> suggestions, CancellationToken cancellationToken = default)
            => _db.EvidenceLinkSuggestions.AddRangeAsync(suggestions, cancellationToken);

        public async Task<IReadOnlyList<EvidenceLinkSuggestion>> ListWithoutRationaleAsync(int take, CancellationToken cancellationToken = default)
            => take <= 0
                ? []
                : await _db.EvidenceLinkSuggestions
                    .Where(s => s.Status == SuggestionStatus.Pending && s.Rationale == null)
                    .OrderBy(s => s.Rank)
                    .ThenBy(s => s.CreatedAt)
                    .Take(take)
                    .ToListAsync(cancellationToken);
    }
}
