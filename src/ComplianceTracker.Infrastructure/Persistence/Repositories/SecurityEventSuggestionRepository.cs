using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of the US-128 repositories. Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class SecurityEventSuggestionRepository : ISecurityEventSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public SecurityEventSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<SecurityEventSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.SecurityEventSuggestions.Where(s => s.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task<SecurityEventSuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
            => _db.SecurityEventSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId && s.ProjectId == projectId, cancellationToken);

        public Task AddAsync(IEnumerable<SecurityEventSuggestion> suggestions, CancellationToken cancellationToken = default)
            => _db.SecurityEventSuggestions.AddRangeAsync(suggestions, cancellationToken);
    }

    public class ExploitedListRepository : IExploitedListRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ExploitedListRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ExploitedList>> ListAsync(CancellationToken cancellationToken = default)
            => await _db.ExploitedLists.ToListAsync(cancellationToken);

        public async Task AddAsync(ExploitedList list, CancellationToken cancellationToken = default)
            => await _db.ExploitedLists.AddAsync(list, cancellationToken);
    }
}
