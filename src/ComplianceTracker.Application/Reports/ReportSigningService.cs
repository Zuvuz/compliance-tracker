using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Reports
{
    // Storage for signatures and seals (US-132, implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IReportSigningRepository
    {
        Task<ReportSigning?> GetAsync(Guid organizationId, Guid snapshotId, CancellationToken cancellationToken = default);

        // The organisation's signings that still wait for a signature
        Task<IReadOnlyList<ReportSigning>> ListOpenAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ReportSigning>> ListAsync(Guid organizationId, IReadOnlyCollection<Guid> snapshotIds, CancellationToken cancellationToken = default);

        Task AddAsync(ReportSigning signing, CancellationToken cancellationToken = default);

        Task AddSealedFileAsync(Guid signingId, byte[] content, CancellationToken cancellationToken = default);

        Task<byte[]?> GetSealedFileAsync(Guid signingId, CancellationToken cancellationToken = default);

        Task<OrganisationSeal?> GetActiveSealAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddSealAsync(OrganisationSeal seal, CancellationToken cancellationToken = default);
    }

    // What a .pfx holds, read and checked by Infrastructure
    public record SealCertificate(string Subject, string Thumbprint, DateTime NotBefore, DateTime NotAfter);

    // One line on the signature page, e.g. "Anna, Compliance officer (Organisation admin), 09-11-2026 10:15 UTC"
    public record SignatureLine(string Name, string Role, DateTime SignedAtUtc);

    // US-132: reads the organisation's certificate and seals a PDF with it (implemented in Infrastructure)
    public interface IReportSealer
    {
        // Throws DomainException when the file is not a certificate with a private key, or the password is wrong
        SealCertificate Read(byte[] pfx, string password);

        // The locked PDF, a page listing the signers and the report's fingerprint, and the digital signature over it all
        byte[] Seal(byte[] lockedPdf, string title, string fingerprint, IReadOnlyList<SignatureLine> signers, byte[] pfx, string password, string organisation, DateTime atUtc);
    }

    // US-132 R2: the person must sign in again before signing
    public class ReauthenticationRequiredException : Exception
    {
        public ReauthenticationRequiredException() : base("Sign in again to sign the report") { }
    }

    // What the Reports tab shows of a locked report's signatures
    public record SigningView(ReportSigning? Signing, IReadOnlyList<(string Name, SignatureRole Role, UserRole? MemberRole, DateTime At)> Signatures,
        IReadOnlyList<SignatureRole> ICanSign, bool CanRequest, bool CanSeal);

    // US-132: signing a locked report (US-050) in the tracker, and sealing it with the organisation's certificate
    public class ReportSigningService
    {
        // R2: a sign-in older than this must be done again before signing
        public static readonly TimeSpan FreshSignIn = TimeSpan.FromMinutes(10);

        private readonly IReportSigningRepository _signings;
        private readonly IReportSnapshotRepository _snapshots;
        private readonly IReportSealer _sealer;
        private readonly ISecretProtector _secrets;
        private readonly CurrentMember _member;
        private readonly ICurrentUser _currentUser;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ReportSigningService(
            IReportSigningRepository signings, IReportSnapshotRepository snapshots, IReportSealer sealer, ISecretProtector secrets,
            CurrentMember member, ICurrentUser currentUser, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _signings = signings;
            _snapshots = snapshots;
            _sealer = sealer;
            _secrets = secrets;
            _member = member;
            _currentUser = currentUser;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ---------------------------------------------------------- The seal (organisation admin)

        public async Task UploadSealAsync(byte[] pfx, string? password, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            if (pfx.Length == 0)
            {
                throw new DomainException("Choose the certificate file (.pfx)");
            }

            var certificate = _sealer.Read(pfx, password ?? "");
            var seal = OrganisationSeal.Create(organization.Id, certificate.Subject, certificate.Thumbprint, certificate.NotBefore, certificate.NotAfter,
                _secrets.Protect(Convert.ToBase64String(pfx)), _secrets.Protect(password ?? ""), (await _member.RequireAsync(cancellationToken)).User.Id, Now);

            if (await _signings.GetActiveSealAsync(organization.Id, cancellationToken) is { } old)
            {
                old.Remove(Now);
            }

            await _signings.AddSealAsync(seal, cancellationToken);
            await _log.LogAsync(ActivityAction.SealUploaded, organization.Id, $"{seal.Subject}, valid until {seal.NotAfter:dd-MM-yyyy}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveSealAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            var seal = await _signings.GetActiveSealAsync(organization.Id, cancellationToken) ?? throw new DomainException("There is no seal");
            seal.Remove(Now);
            await _log.LogAsync(ActivityAction.SealRemoved, organization.Id, seal.Subject, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<OrganisationSeal?> SealAsync(CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? await _signings.GetActiveSealAsync(organization.Id, cancellationToken)
                : null;

        // R3: who signs for the management is chosen per member
        public async Task SetSignsForManagementAsync(Guid memberId, bool signs, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            if (organization.SetSignsForManagement(memberId, signs, user.Id) is { } change)
            {
                var name = organization.Users.First(u => u.Id == memberId).Name;
                await _log.LogAsync(ActivityAction.MemberSignsForManagementChanged, organization.Id, $"{name}: {change}", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // ---------------------------------------------------------- Signing

        public async Task RequestAsync(Guid snapshotId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var snapshot = await _snapshots.GetAsync(organization.Id, snapshotId, cancellationToken) ?? throw new DomainException("Report not found");
            if (await _signings.GetAsync(organization.Id, snapshot.Id, cancellationToken) is not null)
            {
                throw new DomainException("Signatures are asked for already");
            }

            await _signings.AddAsync(ReportSigning.Request(snapshot, user.Id, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.ReportSignatureRequested, snapshot.ProjectId, $"{snapshot.Title}: {string.Join(" and ", ReportSigning.Required.Select(ReportSigning.Title))}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R1–R4, R7. Returns true when the report was sealed too.
        public async Task<bool> SignAsync(Guid snapshotId, SignatureRole role, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            if (_currentUser.SignedInAt is not { } signedInAt || Now - signedInAt > FreshSignIn)
            {
                throw new ReauthenticationRequiredException();
            }

            var snapshot = await _snapshots.GetAsync(organization.Id, snapshotId, cancellationToken) ?? throw new DomainException("Report not found");
            var signing = await _signings.GetAsync(organization.Id, snapshot.Id, cancellationToken) ?? throw new DomainException("Signatures are not asked for yet");
            var content = await _snapshots.GetContentAsync(snapshot.Id, cancellationToken) ?? throw new DomainException("The report's file is missing");

            signing.Sign(role, user, ReportSnapshot.Fingerprint(content), Now);
            await _log.LogAsync(ActivityAction.ReportSigned, snapshot.ProjectId, $"{snapshot.Title}: {ReportSigning.Title(role)}, SHA-256 {snapshot.Sha256}", cancellationToken);

            var sealed_ = signing.IsSigned && await TrySealAsync(organization, snapshot, signing, content, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return sealed_;
        }

        // R4: when the seal was uploaded after the last signature
        public async Task SealReportAsync(Guid snapshotId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var snapshot = await _snapshots.GetAsync(organization.Id, snapshotId, cancellationToken) ?? throw new DomainException("Report not found");
            var signing = await _signings.GetAsync(organization.Id, snapshot.Id, cancellationToken) ?? throw new DomainException("Signatures are not asked for yet");
            var content = await _snapshots.GetContentAsync(snapshot.Id, cancellationToken) ?? throw new DomainException("The report's file is missing");
            if (!await TrySealAsync(organization, snapshot, signing, content, cancellationToken, throwWhenNot: true))
            {
                return;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<(string FileName, byte[] Content)?> SealedFileAsync(Guid snapshotId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _snapshots.GetAsync(organization.Id, snapshotId, cancellationToken) is not { } snapshot
                || await _signings.GetAsync(organization.Id, snapshot.Id, cancellationToken) is not { IsSealed: true } signing
                || await _signings.GetSealedFileAsync(signing.Id, cancellationToken) is not { } content)
            {
                return null;
            }

            return (Path.GetFileNameWithoutExtension(snapshot.FileName) + "-sealed.pdf", content);
        }

        // The Reports tab: per locked report, its signatures and what the member may do
        public async Task<IReadOnlyDictionary<Guid, SigningView>> ForSnapshotsAsync(IReadOnlyCollection<Guid> snapshotIds, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization || snapshotIds.Count == 0)
            {
                return new Dictionary<Guid, SigningView>();
            }

            var (_, user) = await _member.RequireAsync(cancellationToken);
            var signings = (await _signings.ListAsync(organization.Id, snapshotIds, cancellationToken)).ToDictionary(s => s.SnapshotId);
            var hasSeal = await _signings.GetActiveSealAsync(organization.Id, cancellationToken) is { } seal && seal.WhyNotUsable(Now) is null;
            return snapshotIds.ToDictionary(id => id, id =>
            {
                var signing = signings.GetValueOrDefault(id);
                var signatures = signing?.Signatures
                    .Select(s => (organization.Users.FirstOrDefault(u => u.Id == s.SignerId)?.Name ?? "?", s.Role, s.SignerRole, s.SignedAt)).ToList() ?? [];
                var canSign = signing is { IsSealed: false } && signing.Signatures.All(s => s.SignerId != user.Id)
                    ? signing.Missing.Where(r => ReportSigning.MaySign(user, r)).ToList()
                    : [];
                return new SigningView(signing, signatures, canSign,
                    signing is null && user.Can(Permission.DecideCompliance),
                    signing is { IsSigned: true, IsSealed: false } && hasSeal && user.Can(Permission.DecideCompliance));
            });
        }

        // "Needs attention": locked reports waiting for a signature the member may give
        public async Task<IReadOnlyList<(Guid? ProjectId, string Title)>> WaitingForMeAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            var (_, user) = await _member.RequireAsync(cancellationToken);
            var result = new List<(Guid?, string)>();
            foreach (var signing in await _signings.ListOpenAsync(organization.Id, cancellationToken))
            {
                if (signing.Signatures.All(s => s.SignerId != user.Id) && signing.Missing.Any(r => ReportSigning.MaySign(user, r))
                    && await _snapshots.GetAsync(organization.Id, signing.SnapshotId, cancellationToken) is { } snapshot)
                {
                    result.Add((snapshot.ProjectId, snapshot.Title));
                }
            }

            return result;
        }

        private async Task<bool> TrySealAsync(
            Organization organization, ReportSnapshot snapshot, ReportSigning signing, byte[] content, CancellationToken cancellationToken, bool throwWhenNot = false)
        {
            var seal = await _signings.GetActiveSealAsync(organization.Id, cancellationToken);
            var why = seal is null ? "Upload the organisation's seal certificate under Settings to seal the report" : seal.WhyNotUsable(Now);
            if (why is not null)
            {
                // R6: signed, but not sealed; the page says why
                return throwWhenNot ? throw new DomainException(why) : false;
            }

            var lines = signing.Signatures
                .Select(s => new SignatureLine(
                    organization.Users.FirstOrDefault(u => u.Id == s.SignerId)?.Name ?? "?",
                    ReportSigning.Title(s.Role) + (s.SignerRole is { } role ? $" ({RolePermissions.Title(role)})" : ""),
                    s.SignedAt))
                .ToList();
            var pfx = Convert.FromBase64String(_secrets.Unprotect(seal!.ProtectedCertificate));
            var sealedPdf = _sealer.Seal(content, snapshot.Title, snapshot.Sha256, lines, pfx, _secrets.Unprotect(seal.ProtectedPassword), organization.Name, Now);

            signing.Seal(ReportSnapshot.Fingerprint(sealedPdf), seal.Subject, seal.Thumbprint, Now);
            await _signings.AddSealedFileAsync(signing.Id, sealedPdf, cancellationToken);
            await _log.LogAsync(ActivityAction.ReportSealed, snapshot.ProjectId, $"{snapshot.Title}: sealed by {seal.Subject}, SHA-256 {signing.SealedSha256}", cancellationToken);
            return true;
        }
    }
}
