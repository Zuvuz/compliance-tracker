using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-189
    public class GapAnswerRepository : IGapAnswerRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public GapAnswerRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<GapAnswer>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.GapAnswers
                .Where(a => a.OrganizationId == organizationId)
                .OrderByDescending(a => a.AnsweredAt)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(GapAnswer answer, CancellationToken cancellationToken = default)
            => await _db.GapAnswers.AddAsync(answer, cancellationToken);
    }
}
