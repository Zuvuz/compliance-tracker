using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Audits
{
    public enum AuditKind
    {
        Internal,
        External
    }

    public enum AuditResult
    {
        Passed,
        PassedWithRemarks,
        Failed
    }

    // US-134 R3: where a certificate stands on a given day
    public enum CertificateState
    {
        Valid,
        ExpiringSoon,
        Expired
    }

    // US-134: an audit that took place, of a project or of the organisation (its area, B94), with its result, the
    // auditor's signed report and any certificate. It is never changed (R4); a mistake is corrected by a new audit that
    // refers to it. Its findings are followed up one by one (R2).
    public class Audit
    {
        public const int MaxTextLength = 200;
        public const int ExpiryWarningDays = 90;

        private readonly List<AuditFinding> _findings = [];

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public AuditKind Kind { get; private set; }
        // e.g. "ISO/IEC 27001:2022"
        public string Scope { get; private set; } = "";
        public DateOnly HeldOn { get; private set; }
        public string AuditorName { get; private set; } = "";
        public string? AuditorCompany { get; private set; }
        public AuditResult Result { get; private set; }

        // R1: the auditor's report, stored with its fingerprint (as evidence, US-057)
        public string ReportFileName { get; private set; } = "";
        public string ReportSha256 { get; private set; } = "";

        // R3: the certificate, when the audit gave one
        public string? CertificateNumber { get; private set; }
        public string? CertificateIssuer { get; private set; }
        public DateOnly? CertificateValidFrom { get; private set; }
        public DateOnly? CertificateValidTo { get; private set; }

        // R4: the audit this one corrects
        public Guid? CorrectsAuditId { get; private set; }

        public Guid RecordedById { get; private set; }
        public DateTime RecordedAt { get; private set; }

        public IReadOnlyList<AuditFinding> Findings => _findings;

        public bool HasCertificate => CertificateNumber is not null;

        // For EF Core only
        private Audit() { }

        // R1: a date that has been, a scope, an auditor, a result and the report. "today" is the recording day.
        public static Audit Record(
            Guid projectId, AuditKind kind, string? scope, DateOnly heldOn, string? auditorName, string? auditorCompany, AuditResult result,
            string reportFileName, string reportSha256, Guid userId, DateTime at, DateOnly today, Guid? correctsAuditId = null)
        {
            if (!Enum.IsDefined(kind) || !Enum.IsDefined(result))
            {
                throw new DomainException("Choose the kind of audit and its result");
            }

            if (heldOn > today)
            {
                throw new DomainException("An audit is recorded after it took place; the date cannot be in the future");
            }

            return new Audit
            {
                ProjectId = projectId,
                Kind = kind,
                Scope = Required(scope, "Give the scope of the audit, e.g. ISO/IEC 27001:2022"),
                HeldOn = heldOn,
                AuditorName = Required(auditorName, "Give the auditor's name"),
                AuditorCompany = Optional(auditorCompany),
                Result = result,
                ReportFileName = reportFileName,
                ReportSha256 = reportSha256,
                RecordedById = userId,
                RecordedAt = at,
                CorrectsAuditId = correctsAuditId
            };
        }

        // R3: added when the audit is recorded, before it is saved; never changed afterwards
        public void GiveCertificate(string? number, string? issuer, DateOnly validFrom, DateOnly validTo)
        {
            if (HasCertificate)
            {
                throw new DomainException("The audit already has a certificate");
            }

            if (validTo <= validFrom)
            {
                throw new DomainException("The certificate must end after it starts");
            }

            CertificateNumber = Required(number, "Give the certificate's number");
            CertificateIssuer = Optional(issuer) ?? AuditorCompany ?? AuditorName;
            CertificateValidFrom = validFrom;
            CertificateValidTo = validTo;
        }

        // R2: each finding has an owner and a deadline
        public AuditFinding AddFinding(string? title, Guid ownerId, DateOnly dueOn)
        {
            if (dueOn < HeldOn)
            {
                throw new DomainException("A finding's deadline cannot be before the audit");
            }

            var finding = new AuditFinding(Id, ProjectId, Required(title, "Give the finding a title"), ownerId, dueOn);
            _findings.Add(finding);
            return finding;
        }

        // R3: 90 days before the end it needs attention; after the end it has expired
        public CertificateState? CertificateOn(DateOnly day)
            => CertificateValidTo is not { } to ? null
                : day > to ? CertificateState.Expired
                : day > to.AddDays(-ExpiryWarningDays) ? CertificateState.ExpiringSoon
                : CertificateState.Valid;

        // e.g. "ISO/IEC 27001 certified until 30-06-2027" or "ISO/IEC 27001 certificate expired 30-06-2027"
        public string? CertificateBadge(DateOnly day)
            => CertificateOn(day) switch
            {
                null => null,
                CertificateState.Expired => $"{ShortScope} certificate expired {CertificateValidTo:dd-MM-yyyy}",
                _ => $"{ShortScope} certified until {CertificateValidTo:dd-MM-yyyy}"
            };

        // "ISO/IEC 27001:2022" → "ISO/IEC 27001"
        public string ShortScope => Scope.Split(':')[0].Trim();

        private static string Required(string? text, string message)
        {
            var cleaned = text?.Trim();
            if (string.IsNullOrEmpty(cleaned))
            {
                throw new DomainException(message);
            }

            return cleaned.Length <= MaxTextLength ? cleaned : cleaned[..MaxTextLength];
        }

        private static string? Optional(string? text)
            => string.IsNullOrWhiteSpace(text) ? null : text.Trim().Length <= MaxTextLength ? text.Trim() : text.Trim()[..MaxTextLength];
    }

    // US-134 R2: something the audit found, followed up by its owner before the deadline, and closed once with a reason
    public class AuditFinding
    {
        public const int MaxReasonLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid AuditId { get; private set; }
        public Guid ProjectId { get; private set; }
        public string Title { get; private set; } = "";
        public Guid OwnerId { get; private set; }
        public DateOnly DueOn { get; private set; }
        public DateTime? ClosedAt { get; private set; }
        public Guid? ClosedById { get; private set; }
        public string? CloseReason { get; private set; }
        // Evidence of the project that shows it is fixed (optional, B94)
        public Guid? EvidenceId { get; private set; }

        public bool IsOpen => ClosedAt is null;

        public bool IsOverdue(DateOnly today) => IsOpen && today > DueOn;

        // For EF Core only
        private AuditFinding() { }

        internal AuditFinding(Guid auditId, Guid projectId, string title, Guid ownerId, DateOnly dueOn)
        {
            AuditId = auditId;
            ProjectId = projectId;
            Title = title;
            OwnerId = ownerId;
            DueOn = dueOn;
        }

        public void Close(string? reason, Guid? evidenceId, Guid userId, DateTime at)
        {
            if (!IsOpen)
            {
                throw new DomainException("The finding is already closed");
            }

            var cleaned = reason?.Trim();
            if (string.IsNullOrEmpty(cleaned))
            {
                throw new DomainException("Give a reason for closing the finding");
            }

            CloseReason = cleaned.Length <= MaxReasonLength ? cleaned : cleaned[..MaxReasonLength];
            EvidenceId = evidenceId;
            ClosedById = userId;
            ClosedAt = at;
        }
    }

    // R1: the auditor's report, kept next to the audit so the list does not load it
    public class AuditReportFile
    {
        public Guid AuditId { get; private set; }
        public string ContentType { get; private set; } = "";
        public byte[] Content { get; private set; } = [];

        // For EF Core only
        private AuditReportFile() { }

        public AuditReportFile(Guid auditId, EvidenceFile file)
        {
            AuditId = auditId;
            ContentType = file.ContentType;
            Content = file.Content;
        }
    }
}
