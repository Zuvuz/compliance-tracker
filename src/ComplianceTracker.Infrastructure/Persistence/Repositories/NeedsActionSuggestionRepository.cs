using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of INeedsActionSuggestionRepository (US-155). The organisation check happens before, on the project.
    public class NeedsActionSuggestionRepository : INeedsActionSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public NeedsActionSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<NeedsActionSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.NeedsActionSuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task<NeedsActionSuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
            => _db.NeedsActionSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId && s.ProjectId == projectId, cancellationToken);

        public async Task AddRangeAsync(IEnumerable<NeedsActionSuggestion> suggestions, CancellationToken cancellationToken = default)
            => await _db.NeedsActionSuggestions.AddRangeAsync(suggestions, cancellationToken);
    }
}
