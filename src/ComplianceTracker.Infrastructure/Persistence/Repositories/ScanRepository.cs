using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Scanning;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IScanRepository (US-078). The organisation check happens before, on the project.
    public class ScanRepository : IScanRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ScanRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Scan>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.Scans.Where(s => s.ProjectId == projectId).OrderByDescending(s => s.ScannedAt).ToListAsync(cancellationToken);

        public Task<Scan?> GetAsync(Guid projectId, Guid scanId, CancellationToken cancellationToken = default)
            => _db.Scans.FirstOrDefaultAsync(s => s.Id == scanId && s.ProjectId == projectId, cancellationToken);

        public async Task<byte[]?> GetContentAsync(Guid scanId, CancellationToken cancellationToken = default)
            => await _db.ScanFiles.Where(f => f.ScanId == scanId).Select(f => f.Content).FirstOrDefaultAsync(cancellationToken);

        public async Task AddAsync(Scan scan, byte[] content, CancellationToken cancellationToken = default)
        {
            await _db.Scans.AddAsync(scan, cancellationToken);
            await _db.ScanFiles.AddAsync(new ScanFile(scan.Id, content), cancellationToken);
        }

        public async Task<IReadOnlyList<Finding>> ListFindingsAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.Findings.Where(f => f.ProjectId == projectId).ToListAsync(cancellationToken);

        // US-128: for the daily match against the exploited lists
        public async Task<IReadOnlyList<Guid>> ListProjectIdsWithOpenFindingsAsync(CancellationToken cancellationToken = default)
            => await _db.Findings.Where(f => f.State == FindingState.Open).Select(f => f.ProjectId).Distinct().ToListAsync(cancellationToken);

        public async Task AddFindingsAsync(IEnumerable<Finding> findings, CancellationToken cancellationToken = default)
            => await _db.Findings.AddRangeAsync(findings, cancellationToken);

        // US-122 R6
        public Task<int> CountByTokenSinceAsync(Guid tokenId, DateTime since, CancellationToken cancellationToken = default)
            => _db.Scans.CountAsync(s => s.UploadTokenId == tokenId && s.ScannedAt >= since, cancellationToken);
    }

    // EF Core implementation of IScanUploadTokenRepository (US-122)
    public class ScanUploadTokenRepository : IScanUploadTokenRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ScanUploadTokenRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ScanUploadToken>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
            => await _db.ScanUploadTokens.Where(t => t.ProjectId == projectId).ToListAsync(cancellationToken);

        public Task<ScanUploadToken?> GetAsync(Guid projectId, Guid tokenId, CancellationToken cancellationToken = default)
            => _db.ScanUploadTokens.FirstOrDefaultAsync(t => t.Id == tokenId && t.ProjectId == projectId, cancellationToken);

        public Task<ScanUploadToken?> FindByHashAsync(string hash, CancellationToken cancellationToken = default)
            => _db.ScanUploadTokens.FirstOrDefaultAsync(t => t.Hash == hash, cancellationToken);

        public async Task AddAsync(ScanUploadToken token, CancellationToken cancellationToken = default)
            => await _db.ScanUploadTokens.AddAsync(token, cancellationToken);
    }
}
