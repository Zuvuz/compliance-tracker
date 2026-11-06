using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Ai;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IAiEvaluationRepository (US-116)
    public class AiEvaluationRepository : IAiEvaluationRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public AiEvaluationRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<AiEvaluation>> ListAsync(CancellationToken cancellationToken = default)
            => await _db.AiEvaluations.OrderByDescending(e => e.RunAt).ToListAsync(cancellationToken);

        public async Task AddAsync(AiEvaluation evaluation, CancellationToken cancellationToken = default)
            => await _db.AiEvaluations.AddAsync(evaluation, cancellationToken);
    }
}
