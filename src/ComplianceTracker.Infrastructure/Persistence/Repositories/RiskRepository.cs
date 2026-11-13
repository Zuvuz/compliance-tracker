using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IRiskRepository (US-042).
    // The organisation check happens before, on the project (RiskService, RiskQueries).
    // US-049: imports waiting to be confirmed. The organisation check happens before, on the project.
    public class RiskImportRepository : IRiskImportRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RiskImportRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(RiskImport import, byte[] content, CancellationToken cancellationToken = default)
        {
            await _db.RiskImports.AddAsync(import, cancellationToken);
            await _db.RiskImportFiles.AddAsync(new RiskImportFile(import.Id, content), cancellationToken);
        }

        public Task<RiskImport?> GetAsync(Guid projectId, Guid importId, CancellationToken cancellationToken = default)
            => _db.RiskImports.FirstOrDefaultAsync(i => i.Id == importId && i.ProjectId == projectId, cancellationToken);

        public async Task<byte[]?> GetContentAsync(Guid importId, CancellationToken cancellationToken = default)
            => await _db.RiskImportFiles.Where(f => f.ImportId == importId).Select(f => f.Content).FirstOrDefaultAsync(cancellationToken);
    }

    // US-112: the AI agent's drafts
    public class RiskDraftRepository : IRiskDraftRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RiskDraftRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<RiskDraft>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.RiskDrafts.Where(d => d.ProjectId == projectId).ToListAsync(cancellationToken);

        public async Task AddRangeAsync(IEnumerable<RiskDraft> drafts, CancellationToken cancellationToken = default)
            => await _db.RiskDrafts.AddRangeAsync(drafts, cancellationToken);
    }

    public class RiskRepository : IRiskRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public RiskRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Risk>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.Risks.Where(r => r.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task<Risk?> GetAsync(Guid projectId, Guid riskId, CancellationToken cancellationToken = default)
            => _db.Risks.FirstOrDefaultAsync(r => r.Id == riskId && r.ProjectId == projectId, cancellationToken);

        public Task<Risk?> GetByIdAsync(Guid riskId, CancellationToken cancellationToken = default)
            => _db.Risks.FirstOrDefaultAsync(r => r.Id == riskId, cancellationToken);

        public async Task AddAsync(Risk risk, CancellationToken cancellationToken = default)
            => await _db.Risks.AddAsync(risk, cancellationToken);
    }
}
