using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IMappingSuggestionRepository (US-020, US-036).
    // The organisation check happens before, on the project; the export filters on the organisation's projects.
    public class MappingSuggestionRepository : IMappingSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public MappingSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<MappingSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.MappingSuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task<MappingSuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
            => _db.MappingSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId && s.ProjectId == projectId, cancellationToken);

        public async Task AddRangeAsync(IEnumerable<MappingSuggestion> suggestions, CancellationToken cancellationToken = default)
            => await _db.MappingSuggestions.AddRangeAsync(suggestions, cancellationToken);

        // Oldest first, best ranked first, so the top suggestions of a run get their rationale first
        public async Task<IReadOnlyList<MappingSuggestion>> ListWithoutRationaleAsync(int take, CancellationToken cancellationToken = default)
            => await _db.MappingSuggestions
                .Where(s => s.Status == SuggestionStatus.Pending && s.Rationale == null)
                .OrderBy(s => s.CreatedAt).ThenBy(s => s.Rank)
                .Take(take)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<MappingSuggestion>> ListDecidedAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.MappingSuggestions
                .AsNoTracking()
                .Where(s => (s.Status == SuggestionStatus.Approved || s.Status == SuggestionStatus.Rejected)
                    && _db.Projects.Any(p => p.Id == s.ProjectId && p.OrganizationId == organizationId))
                .OrderBy(s => s.DecidedAt)
                .ToListAsync(cancellationToken);
    }
}
