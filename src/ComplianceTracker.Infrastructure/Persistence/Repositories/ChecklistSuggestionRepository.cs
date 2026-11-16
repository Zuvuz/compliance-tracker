using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-193
    public class ChecklistSuggestionRepository : IChecklistSuggestionRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ChecklistSuggestionRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ChecklistSuggestion>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.ChecklistSuggestions.Where(s => s.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddRangeAsync(IEnumerable<ChecklistSuggestion> suggestions, CancellationToken cancellationToken = default)
            => await _db.ChecklistSuggestions.AddRangeAsync(suggestions, cancellationToken);
    }
}
