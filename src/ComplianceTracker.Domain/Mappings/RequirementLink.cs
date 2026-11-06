using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Mappings
{
    // A link saying that a user story in a project helps meet a requirement (US-021).
    // Made by hand for now; AI suggestions (US-020) will create the same links once a person approves them (B13).
    // Its own aggregate, like WorkItem. Unlinking sets RemovedAt; a link is never deleted, so the history is kept.
    public class RequirementLink
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid RequirementId { get; private set; }
        public Guid WorkItemId { get; private set; }

        public LinkSource Source { get; private set; }

        public Guid LinkedById { get; private set; }

        // US-010: the role of the person when they linked it
        public UserRole? LinkedRole { get; private set; }
        public DateTime LinkedAt { get; private set; }

        public Guid? RemovedById { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        // Linked and not removed. Only active links count as coverage.
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
        private RequirementLink() { }

        // The caller checks that the requirement belongs to a framework version the project follows,
        // that the work item belongs to the project, and that the pair is not linked already
        // source: Manual, or AiApproved when a person approved an AI suggestion (US-020 R6)
        public static RequirementLink Create(
            Guid projectId, Guid requirementId, Guid workItemId, Guid linkedById, DateTime at, LinkSource source = LinkSource.Manual,
            UserRole? role = null)
            => new()
            {
                LinkedRole = role,
                ProjectId = projectId,
                RequirementId = requirementId,
                WorkItemId = workItemId,
                Source = source,
                LinkedById = linkedById,
                LinkedAt = at
            };

        // US-069 R6: the same link on the requirement with the same code in the version the project moves to.
        // Who linked it, with which role and when, is kept, so the history still says who decided.
        // US-110 R2: textChanged marks it for review; one still waiting for review keeps waiting, against its first text.
        public RequirementLink CarryTo(Guid requirementId, bool textChanged = false)
            => new()
            {
                ProjectId = ProjectId,
                RequirementId = requirementId,
                WorkItemId = WorkItemId,
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
                throw new DomainException("The link is already removed");
            }

            RemovedById = userId;
            RemovedAt = at;
        }
    }

    // How a link was made. Stored as text, so AI-approved links (US-020) can be added later.
    public enum LinkSource
    {
        Manual,

        // A person approved an AI suggestion (US-020)
        AiApproved
    }
}
