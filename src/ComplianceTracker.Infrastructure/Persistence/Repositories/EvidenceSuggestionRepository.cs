using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IEvidenceSuggestionRepository (US-058). Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class EvidenceSuggestionRepository : IEvidenceSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public EvidenceSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<EvidenceSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.EvidenceSuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task AddRangeAsync(IEnumerable<EvidenceSuggestion> suggestions, CancellationToken cancellationToken = default)
            => _db.EvidenceSuggestions.AddRangeAsync(suggestions, cancellationToken);
    }
}
