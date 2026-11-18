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

        // #52
        public async Task<IReadOnlyList<TestRunUpload>> ListUploadsAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.TestRunUploads.Where(u => u.ProjectId == projectId).OrderByDescending(u => u.UploadedAt).ToListAsync(cancellationToken);

        public async Task AddUploadAsync(TestRunUpload upload, CancellationToken cancellationToken = default)
            => await _db.TestRunUploads.AddAsync(upload, cancellationToken);
    }
}
