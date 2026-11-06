using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-059: a link saying that one evidence of a project proves one requirement (R2). Its own aggregate, like
    // RequirementLink (US-021). Unlinking sets RemovedAt; a link is never deleted, so the history is kept (R7).
    public class EvidenceLink
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid EvidenceId { get; private set; }
        public Guid RequirementId { get; private set; }

        // Manual, or AiApproved when a person approved an AI suggestion (R4)
        public LinkSource Source { get; private set; }

        public Guid LinkedById { get; private set; }

        // US-010: the role of the person when they linked it
        public UserRole? LinkedRole { get; private set; }
        public DateTime LinkedAt { get; private set; }

        public Guid? RemovedById { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        // US-110: carried over to a requirement whose title or text changed in the new version. It still shows, but
        // does not count until a person has checked it against the new text. CarriedFromRequirementId is the
        // requirement it was carried from, so the old text can be shown next to the new.
        public bool NeedsReview { get; private set; }
        public Guid? CarriedFromRequirementId { get; private set; }
        public Guid? ReviewedById { get; private set; }
        public DateTime? ReviewedAt { get; private set; }

        // US-110 R5: "Still correct"
        public void MarkReviewed(Guid userId, DateTime at)
        {
            if (!NeedsReview)
            {
                throw new DomainException("Nothing to review");
            }

            NeedsReview = false;
            ReviewedById = userId;
            ReviewedAt = at;
        }

        // For EF Core only
        private EvidenceLink() { }

        // The caller checks that the evidence and the requirement belong to the project, and that the pair is not
        // linked already (R2)
        public static EvidenceLink Create(
            Guid projectId, Guid evidenceId, Guid requirementId, Guid linkedById, UserRole? role, DateTime at,
            LinkSource source = LinkSource.Manual)
            => new()
            {
                ProjectId = projectId,
                EvidenceId = evidenceId,
                RequirementId = requirementId,
                Source = source,
                LinkedById = linkedById,
                LinkedRole = role,
                LinkedAt = at
            };

        // US-069 R6: the same link on the requirement with the same code in the version the project moves to.
        // Who linked it, with which role and when, is kept.
        // US-110 R2: textChanged marks it for review
        public EvidenceLink CarryTo(Guid requirementId, bool textChanged = false)
            => new()
            {
                ProjectId = ProjectId,
                EvidenceId = EvidenceId,
                RequirementId = requirementId,
                Source = Source,
                LinkedById = LinkedById,
                LinkedRole = LinkedRole,
                LinkedAt = LinkedAt,
                NeedsReview = NeedsReview || textChanged,
                CarriedFromRequirementId = NeedsReview ? CarriedFromRequirementId : RequirementId
            };

        public void Remove(Guid userId, DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The evidence is not linked to the requirement");
            }

            RemovedById = userId;
            RemovedAt = at;
        }
    }
}
