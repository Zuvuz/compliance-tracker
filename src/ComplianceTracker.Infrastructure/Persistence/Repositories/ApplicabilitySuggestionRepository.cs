using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IApplicabilitySuggestionRepository (US-060). The organisation check happens before, on the project.
    public class ApplicabilitySuggestionRepository : IApplicabilitySuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ApplicabilitySuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ApplicabilitySuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.ApplicabilitySuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task<ApplicabilitySuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
            => _db.ApplicabilitySuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId && s.ProjectId == projectId, cancellationToken);

        public async Task AddRangeAsync(IEnumerable<ApplicabilitySuggestion> suggestions, CancellationToken cancellationToken = default)
            => await _db.ApplicabilitySuggestions.AddRangeAsync(suggestions, cancellationToken);
    }
}
