using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of the US-132 repository. Nothing is saved until IUnitOfWork.SaveChangesAsync.
    public class ReportSigningRepository : IReportSigningRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ReportSigningRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public Task<ReportSigning?> GetAsync(Guid organizationId, Guid snapshotId, CancellationToken cancellationToken = default)
            => _db.ReportSignings.Include(s => s.Signatures)
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.SnapshotId == snapshotId, cancellationToken);

        public async Task<IReadOnlyList<ReportSigning>> ListOpenAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => (await _db.ReportSignings.Include(s => s.Signatures)
                    .Where(s => s.OrganizationId == organizationId && s.SealedAt == null)
                    .ToListAsync(cancellationToken))
                .Where(s => !s.IsSigned)
                .ToList();

        public async Task<IReadOnlyList<ReportSigning>> ListAsync(Guid organizationId, IReadOnlyCollection<Guid> snapshotIds, CancellationToken cancellationToken = default)
            => await _db.ReportSignings.Include(s => s.Signatures)
                .Where(s => s.OrganizationId == organizationId && snapshotIds.Contains(s.SnapshotId))
                .ToListAsync(cancellationToken);

        public async Task AddAsync(ReportSigning signing, CancellationToken cancellationToken = default)
            => await _db.ReportSignings.AddAsync(signing, cancellationToken);

        public async Task AddSealedFileAsync(Guid signingId, byte[] content, CancellationToken cancellationToken = default)
            => await _db.SealedReportFiles.AddAsync(new SealedReportFile { SigningId = signingId, Content = content }, cancellationToken);

        public Task<byte[]?> GetSealedFileAsync(Guid signingId, CancellationToken cancellationToken = default)
            => _db.SealedReportFiles.Where(f => f.SigningId == signingId).Select(f => f.Content).FirstOrDefaultAsync(cancellationToken)!;

        public Task<OrganisationSeal?> GetActiveSealAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => _db.OrganisationSeals.FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.RemovedAt == null, cancellationToken);

        public async Task AddSealAsync(OrganisationSeal seal, CancellationToken cancellationToken = default)
            => await _db.OrganisationSeals.AddAsync(seal, cancellationToken);
    }
}
