using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-038 R1: what the AI says one piece of evidence does for one requirement
    public enum EvidenceVerdict
    {
        Covers,
        PartlyCovers,
        DoesNotCover
    }

    public enum EvidenceCheckStatus
    {
        // AI-generated: shown, but counts for nothing until a person decides (R2)
        Pending,
        Confirmed,
        Overruled
    }

    // US-038: the AI's check of one piece of evidence against one requirement, and the person's decision. It belongs to the
    // evidence's fingerprint at the time (R4); a newer check of the same pair replaces the older one on the page, and both
    // are kept, as every suggestion and decision is (US-036, R5).
    public class EvidenceCheck
    {
        public const int MaxTextLength = 2000;
        public const int MaxReasonLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid EvidenceId { get; private set; }
        public Guid RequirementId { get; private set; }

        // R1: the AI's answer
        public EvidenceVerdict AiVerdict { get; private set; }
        public string Missing { get; private set; } = "";
        public string Quotes { get; private set; } = "";

        // R4: the evidence as it was checked (its SHA-256, or its title for a link)
        public string EvidenceFingerprint { get; private set; } = "";
        public string Model { get; private set; } = "";
        public string PromptVersion { get; private set; } = "";
        public Guid CheckedById { get; private set; }
        public DateTime CheckedAt { get; private set; }

        // R2: the person's decision
        public EvidenceCheckStatus Status { get; private set; } = EvidenceCheckStatus.Pending;
        public EvidenceVerdict? DecidedVerdict { get; private set; }
        public string? DecisionReason { get; private set; }
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }

        public bool IsDecided => Status != EvidenceCheckStatus.Pending;

        // R3: the evidence does not count for the requirement once a person agrees it does not cover it
        public bool RefutesEvidence => DecidedVerdict == EvidenceVerdict.DoesNotCover;

        // For EF Core only
        private EvidenceCheck() { }

        public static EvidenceCheck Record(
            Guid projectId, Guid evidenceId, Guid requirementId, EvidenceVerdict verdict, string? missing, string? quotes,
            string evidenceFingerprint, string model, string promptVersion, Guid userId, DateTime at)
        {
            if (!Enum.IsDefined(verdict))
            {
                throw new DomainException("The AI gave no verdict");
            }

            return new EvidenceCheck
            {
                ProjectId = projectId,
                EvidenceId = evidenceId,
                RequirementId = requirementId,
                AiVerdict = verdict,
                Missing = Cut(missing),
                Quotes = Cut(quotes),
                EvidenceFingerprint = evidenceFingerprint,
                Model = model,
                PromptVersion = promptVersion,
                CheckedById = userId,
                CheckedAt = at
            };
        }

        // R2: a person agrees with the AI
        public void Confirm(Guid userId, UserRole? role, DateTime at)
        {
            EnsurePending();
            Status = EvidenceCheckStatus.Confirmed;
            DecidedVerdict = AiVerdict;
            Decided(userId, role, at);
        }

        // R2: a person says otherwise, with another verdict and a reason
        public void Overrule(EvidenceVerdict? verdict, string? reason, Guid userId, UserRole? role, DateTime at)
        {
            EnsurePending();
            if (verdict is not { } chosen || !Enum.IsDefined(chosen))
            {
                throw new DomainException("Choose the verdict");
            }

            if (chosen == AiVerdict)
            {
                throw new DomainException("That is the AI's verdict; confirm it instead");
            }

            var cleaned = reason?.Trim();
            if (string.IsNullOrEmpty(cleaned))
            {
                throw new DomainException("Give a reason for overruling the AI");
            }

            Status = EvidenceCheckStatus.Overruled;
            DecidedVerdict = chosen;
            DecisionReason = cleaned.Length <= MaxReasonLength ? cleaned : cleaned[..MaxReasonLength];
            Decided(userId, role, at);
        }

        // e.g. "Covers", "Partly covers", "Does not cover"
        public static string Text(EvidenceVerdict verdict) => verdict switch
        {
            EvidenceVerdict.Covers => "Covers",
            EvidenceVerdict.PartlyCovers => "Partly covers",
            _ => "Does not cover"
        };

        private void Decided(Guid userId, UserRole? role, DateTime at)
        {
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        private void EnsurePending()
        {
            if (IsDecided)
            {
                throw new DomainException("The check has already been decided");
            }
        }

        private static string Cut(string? text)
        {
            var cleaned = text?.Trim() ?? "";
            return cleaned.Length <= MaxTextLength ? cleaned : cleaned[..MaxTextLength];
        }
    }
}
