using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Organizations
{
    // US-195: the kinds of date a form asks for, each with the organisation's default number of months
    public enum ReviewDateKind
    {
        EvidenceValidity,
        DocumentReview,
        SupplierEvidence,
        AuditCertificate
    }

    // US-195 R1, R3: the organisation's defaults, in months from today. Pure.
    public sealed record ReviewDefaults(int EvidenceValidity, int DocumentReview, int SupplierEvidence, int AuditCertificate)
    {
        public const int MinMonths = 1;
        public const int MaxMonths = 60;

        public static readonly ReviewDefaults Standard = new(12, 12, 12, 36);

        public int MonthsFor(ReviewDateKind kind) => kind switch
        {
            ReviewDateKind.EvidenceValidity => EvidenceValidity,
            ReviewDateKind.DocumentReview => DocumentReview,
            ReviewDateKind.SupplierEvidence => SupplierEvidence,
            ReviewDateKind.AuditCertificate => AuditCertificate,
            _ => 12
        };

        // R1: e.g. 15-11-2026 with 12 months → 15-11-2027; 29-02-2028 with 12 months → 28-02-2029
        public DateOnly DateFor(ReviewDateKind kind, DateOnly today) => today.AddMonths(MonthsFor(kind));

        public static string Title(ReviewDateKind kind) => kind switch
        {
            ReviewDateKind.EvidenceValidity => "Evidence is valid for",
            ReviewDateKind.DocumentReview => "Policies and procedures are reviewed every",
            ReviewDateKind.SupplierEvidence => "Supplier evidence is valid for",
            ReviewDateKind.AuditCertificate => "Audit certificates are valid for",
            _ => kind.ToString()
        };

        // R3: each from 1 to 60 months
        public ReviewDefaults Checked()
        {
            foreach (var months in new[] { EvidenceValidity, DocumentReview, SupplierEvidence, AuditCertificate })
            {
                if (months < MinMonths || months > MaxMonths)
                {
                    throw new DomainException($"Choose from {MinMonths} to {MaxMonths} months");
                }
            }

            return this;
        }

        // R2: what the form says when a date is left empty
        public const string NoDateWarning = "No review date: it will never remind anyone";
    }
}
