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
        public DateTime LinkedAt { get; private set; }

        public Guid? RemovedById { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        // Linked and not removed. Only active links count as coverage.
        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private RequirementLink() { }

        // The caller checks that the requirement belongs to a framework version the project follows,
        // that the work item belongs to the project, and that the pair is not linked already
        public static RequirementLink Create(Guid projectId, Guid requirementId, Guid workItemId, Guid linkedById, DateTime at)
            => new()
            {
                ProjectId = projectId,
                RequirementId = requirementId,
                WorkItemId = workItemId,
                Source = LinkSource.Manual,
                LinkedById = linkedById,
                LinkedAt = at
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
        Manual
    }
}
