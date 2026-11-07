using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Reports
{
    // US-132: the roles a locked report is signed in
    public enum SignatureRole
    {
        // A member whose role may decide compliance (Admin or Compliance manager)
        ComplianceOfficer,

        // A member the organisation has marked as signing for the management
        Manager
    }

    // US-132: the signing of one locked report (US-050). Both roles must sign, by two different people (R3). When both
    // have signed, the PDF is sealed with the organisation's certificate (R4). The report itself never changes; the
    // sealed PDF is a new file that names the signers and the report's fingerprint.
    public class ReportSigning
    {
        public static readonly IReadOnlyList<SignatureRole> Required = [SignatureRole.ComplianceOfficer, SignatureRole.Manager];

        private readonly List<ReportSignature> _signatures = [];

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid SnapshotId { get; private set; }

        // The locked report's fingerprint when signing was asked for; every signature is for exactly this file
        public string SnapshotSha256 { get; private set; } = "";
        public Guid RequestedById { get; private set; }
        public DateTime RequestedAt { get; private set; }

        public IReadOnlyList<ReportSignature> Signatures => _signatures;

        // R4: set when the PDF was sealed
        public DateTime? SealedAt { get; private set; }
        public string? SealedSha256 { get; private set; }
        public string? SealSubject { get; private set; }
        public string? SealThumbprint { get; private set; }

        public bool IsSigned => Required.All(r => _signatures.Any(s => s.Role == r));
        public bool IsSealed => SealedAt is not null;

        public IReadOnlyList<SignatureRole> Missing => Required.Where(r => _signatures.All(s => s.Role != r)).ToList();

        private ReportSigning() { }

        public static ReportSigning Request(ReportSnapshot snapshot, Guid requestedById, DateTime at)
            => new()
            {
                OrganizationId = snapshot.OrganizationId,
                SnapshotId = snapshot.Id,
                SnapshotSha256 = snapshot.Sha256,
                RequestedById = requestedById,
                RequestedAt = at
            };

        // R2 is checked by the caller (a fresh sign-in); R3 here: each role once, never one person in two roles
        public ReportSignature Sign(SignatureRole role, User signer, string sha256OfSignedFile, DateTime at)
        {
            if (IsSealed)
            {
                throw new DomainException("The report is sealed already");
            }

            if (sha256OfSignedFile != SnapshotSha256)
            {
                throw new DomainException("The file is not the locked report");
            }

            if (_signatures.Any(s => s.Role == role))
            {
                throw new DomainException($"The report is signed as {Title(role)} already");
            }

            if (_signatures.Any(s => s.SignerId == signer.Id))
            {
                throw new DomainException("You have signed this report already; the other role must be signed by another person");
            }

            if (!MaySign(signer, role))
            {
                throw new DomainException($"You may not sign as {Title(role)}");
            }

            var signature = new ReportSignature(Id, role, signer.Id, signer.Role, at);
            _signatures.Add(signature);
            return signature;
        }

        // R4
        public void Seal(string sealedSha256, string subject, string thumbprint, DateTime at)
        {
            if (!IsSigned)
            {
                throw new DomainException($"The report needs the signature of {string.Join(" and ", Missing.Select(Title))} first");
            }

            if (IsSealed)
            {
                throw new DomainException("The report is sealed already");
            }

            SealedAt = at;
            SealedSha256 = sealedSha256;
            SealSubject = subject;
            SealThumbprint = thumbprint;
        }

        public static bool MaySign(User member, SignatureRole role) => member.IsActive && role switch
        {
            SignatureRole.ComplianceOfficer => member.Can(Permission.DecideCompliance),
            _ => member.SignsForManagement
        };

        public static string Title(SignatureRole role) => role == SignatureRole.ComplianceOfficer ? "Compliance officer" : "Manager";
    }

    // US-132 R1: who signed, in which role, with the member's role then, and when
    public class ReportSignature
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid SigningId { get; private set; }
        public SignatureRole Role { get; private set; }
        public Guid SignerId { get; private set; }
        public UserRole? SignerRole { get; private set; }
        public DateTime SignedAt { get; private set; }

        private ReportSignature() { }

        internal ReportSignature(Guid signingId, SignatureRole role, Guid signerId, UserRole? signerRole, DateTime at)
        {
            SigningId = signingId;
            Role = role;
            SignerId = signerId;
            SignerRole = signerRole;
            SignedAt = at;
        }
    }

    // US-132: the organisation's certificate for sealing reports. Uploaded by the organisation admin as a .pfx; the file
    // and its password are kept encrypted and never shown again. One active seal per organisation.
    public class OrganisationSeal
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Subject { get; private set; } = "";
        public string Thumbprint { get; private set; } = "";
        public DateTime NotBefore { get; private set; }
        public DateTime NotAfter { get; private set; }

        // Encrypted with the app's data protection (ISecretProtector)
        public string ProtectedCertificate { get; private set; } = "";
        public string ProtectedPassword { get; private set; } = "";

        public Guid UploadedById { get; private set; }
        public DateTime UploadedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        private OrganisationSeal() { }

        public static OrganisationSeal Create(
            Guid organizationId, string subject, string thumbprint, DateTime notBefore, DateTime notAfter,
            string protectedCertificate, string protectedPassword, Guid uploadedById, DateTime at)
        {
            if (notAfter <= at)
            {
                throw new DomainException($"The certificate expired on {notAfter:dd-MM-yyyy}");
            }

            return new OrganisationSeal
            {
                OrganizationId = organizationId,
                Subject = subject,
                Thumbprint = thumbprint,
                NotBefore = notBefore,
                NotAfter = notAfter,
                ProtectedCertificate = protectedCertificate,
                ProtectedPassword = protectedPassword,
                UploadedById = uploadedById,
                UploadedAt = at
            };
        }

        // R6: an expired (or not yet valid) certificate cannot seal
        public string? WhyNotUsable(DateTime at)
            => !IsActive ? "The seal is removed"
                : at < NotBefore ? $"The certificate is valid from {NotBefore:dd-MM-yyyy}"
                : at >= NotAfter ? $"The certificate expired on {NotAfter:dd-MM-yyyy}"
                : null;

        public void Remove(DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The seal is removed already");
            }

            RemovedAt = at;
        }
    }
}
