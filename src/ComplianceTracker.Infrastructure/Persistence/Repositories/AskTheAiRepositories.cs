using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementations of US-106's repositories. Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class StoryDraftRepository : IStoryDraftRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public StoryDraftRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<StoryDraft>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.StoryDrafts.Where(d => d.ProjectId == projectId).OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);

        public Task AddAsync(StoryDraft draft, CancellationToken cancellationToken = default)
            => _db.StoryDrafts.AddAsync(draft, cancellationToken).AsTask();
    }

    public class RiskMeasureSuggestionRepository : IRiskMeasureSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RiskMeasureSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<RiskMeasureSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.RiskMeasureSuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task AddRangeAsync(IEnumerable<RiskMeasureSuggestion> suggestions, CancellationToken cancellationToken = default)
            => _db.RiskMeasureSuggestions.AddRangeAsync(suggestions, cancellationToken);
    }
}
