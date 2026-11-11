using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IEvidenceStoryRepository (US-146). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class EvidenceStoryRepository : IEvidenceStoryRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public EvidenceStoryRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<EvidenceStoryLink>> ListActiveLinksAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.EvidenceStoryLinks.Where(l => l.ProjectId == projectId && l.RemovedAt == null).ToListAsync(cancellationToken);

        public Task AddLinkAsync(EvidenceStoryLink link, CancellationToken cancellationToken = default)
            => _db.EvidenceStoryLinks.AddAsync(link, cancellationToken).AsTask();

        public async Task<IReadOnlyList<EvidenceStorySuggestion>> ListSuggestionsAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.EvidenceStorySuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task AddSuggestionsAsync(IEnumerable<EvidenceStorySuggestion> suggestions, CancellationToken cancellationToken = default)
            => _db.EvidenceStorySuggestions.AddRangeAsync(suggestions, cancellationToken);
    }
}
