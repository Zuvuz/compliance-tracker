using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Organizations
{
    // US-191 R1: how much the organisation depends on the supplier. High and Medium need evidence; Low does not.
    public enum SupplierCriticality
    {
        High,
        Medium,
        Low
    }

    public enum SupplierEvidenceKind
    {
        Certificate,
        DataProcessingAgreement,
        SecurityQuestionnaire,
        AuditReport,
        Contract,
        Other
    }

    // US-191: a supplier of the organisation, with what it delivers, how critical it is and which rules it must meet for
    // the organisation (R2). Removing keeps the row and its evidence, so the history stays.
    public class Supplier
    {
        public const int MaxNameLength = 200;
        public const int MaxTextLength = 500;

        // R2: the rules a supplier can be followed up on; the first four are the ones the tracker knows today
        public static readonly IReadOnlyList<string> KnownRules =
        [
            "NIS2 Art. 21(2)(d)",
            "GDPR Art. 28",
            "ISO/IEC 27001 A.5.19–A.5.22",
            "CRA Art. 13(5)",
            "DORA Art. 28"
        ];

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = "";
        public string Delivers { get; private set; } = "";
        public SupplierCriticality Criticality { get; private set; }
        public string? Contact { get; private set; }

        // R2: e.g. "NIS2 Art. 21(2)(d); GDPR Art. 28"
        public string Rules { get; private set; } = "";

        public Guid AddedById { get; private set; }
        public DateTime AddedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        private readonly List<SupplierEvidence> _evidence = [];
        public IReadOnlyList<SupplierEvidence> Evidence => _evidence;

        public bool IsActive => RemovedAt is null;

        public IReadOnlyList<string> RuleList => Rules.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // For EF Core only
        private Supplier() { }

        public static Supplier Create(Guid organizationId, string? name, string? delivers, SupplierCriticality? criticality, string? contact,
            IEnumerable<string>? rules, Guid addedById, DateTime at)
        {
            var supplier = new Supplier { OrganizationId = organizationId, AddedById = addedById, AddedAt = at };
            supplier.Change(name, delivers, criticality, contact, rules);
            return supplier;
        }

        public void Change(string? name, string? delivers, SupplierCriticality? criticality, string? contact, IEnumerable<string>? rules)
        {
            var cleanName = (name ?? "").Trim();
            if (cleanName.Length == 0)
            {
                throw new DomainException("Give the supplier's name");
            }

            if (cleanName.Length > MaxNameLength)
            {
                throw new DomainException($"The name must be {MaxNameLength} characters or fewer");
            }

            if (criticality is null || !Enum.IsDefined(criticality.Value))
            {
                throw new DomainException("Choose how critical the supplier is");
            }

            var chosen = (rules ?? []).Select(r => r.Trim()).Where(r => r.Length > 0).Distinct().ToList();
            if (chosen.Any(r => !KnownRules.Contains(r)))
            {
                throw new DomainException("Choose rules from the list");
            }

            Name = cleanName;
            Delivers = Cut(delivers);
            Criticality = criticality.Value;
            Contact = string.IsNullOrWhiteSpace(contact) ? null : Cut(contact);
            Rules = string.Join("; ", chosen);
        }

        public SupplierEvidence AddEvidence(SupplierEvidenceKind? kind, string? title, DateOnly? validUntil, string fileName, string contentType,
            long size, string sha256, Guid addedById, DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The supplier has been removed");
            }

            var evidence = SupplierEvidence.Create(Id, kind, title, validUntil, fileName, contentType, size, sha256, addedById, at);
            _evidence.Add(evidence);
            return evidence;
        }

        public void Remove(DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The supplier has been removed");
            }

            RemovedAt = at;
        }

        private static string Cut(string? text)
        {
            var clean = (text ?? "").Trim();
            return clean.Length <= MaxTextLength ? clean : clean[..MaxTextLength];
        }
    }

    // US-191: a certificate, data processing agreement, questionnaire or audit report from a supplier, with its end date
    public class SupplierEvidence
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid SupplierId { get; private set; }
        public SupplierEvidenceKind Kind { get; private set; }
        public string Title { get; private set; } = "";
        public DateOnly? ValidUntil { get; private set; }
        public string FileName { get; private set; } = "";
        public string ContentType { get; private set; } = "";
        public long SizeInBytes { get; private set; }
        public string Sha256 { get; private set; } = "";
        public Guid AddedById { get; private set; }
        public DateTime AddedAt { get; private set; }

        // R3: valid on the day, and not past its end date
        public bool IsValidOn(DateOnly day) => ValidUntil is null || ValidUntil >= day;

        // For EF Core only
        private SupplierEvidence() { }

        internal static SupplierEvidence Create(Guid supplierId, SupplierEvidenceKind? kind, string? title, DateOnly? validUntil, string fileName,
            string contentType, long size, string sha256, Guid addedById, DateTime at)
        {
            if (kind is null || !Enum.IsDefined(kind.Value))
            {
                throw new DomainException("Choose what kind of evidence it is");
            }

            var cleanTitle = (title ?? "").Trim();
            if (cleanTitle.Length == 0)
            {
                throw new DomainException("Give the evidence a title, e.g. ISO/IEC 27001 certificate");
            }

            return new SupplierEvidence
            {
                SupplierId = supplierId,
                Kind = kind.Value,
                Title = cleanTitle.Length <= Supplier.MaxNameLength ? cleanTitle : cleanTitle[..Supplier.MaxNameLength],
                ValidUntil = validUntil,
                FileName = fileName,
                ContentType = contentType,
                SizeInBytes = size,
                Sha256 = sha256,
                AddedById = addedById,
                AddedAt = at
            };
        }
    }

    // US-191 R1, R3: a supplier's status on a day. Pure.
    public enum SupplierState
    {
        Ok,
        ExpiresSoon,
        Missing
    }

    public sealed record SupplierStatus(SupplierState State, string Detail)
    {
        public const int SoonDays = 30;

        public static SupplierStatus Of(Supplier supplier, DateOnly today)
        {
            if (supplier.Criticality == SupplierCriticality.Low)
            {
                return new SupplierStatus(SupplierState.Ok, "Low criticality: no evidence needed");
            }

            var valid = supplier.Evidence.Where(e => e.IsValidOn(today)).ToList();
            var expired = supplier.Evidence.Where(e => !e.IsValidOn(today)).Select(e => e.Title).Distinct().ToList();
            if (valid.Count == 0)
            {
                return new SupplierStatus(SupplierState.Missing,
                    expired.Count == 0 ? "No evidence" : string.Join(", ", expired.Select(t => $"{t} expired")));
            }

            // R3: soon when every valid piece ends within the next 30 days
            var lasts = valid.Any(e => e.ValidUntil is null) ? (DateOnly?)null : valid.Max(e => e.ValidUntil);
            return lasts is { } end && end <= today.AddDays(SoonDays)
                ? new SupplierStatus(SupplierState.ExpiresSoon, $"Evidence ends {end:dd-MM-yyyy}")
                : new SupplierStatus(SupplierState.Ok, $"{valid.Count} valid");
        }
    }
}
