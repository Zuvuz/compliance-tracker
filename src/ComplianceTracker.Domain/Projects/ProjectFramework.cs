namespace ComplianceTracker.Domain.Projects
{
    // One framework version that a project must follow (US-011).
    // Removing it, or moving to another version, sets RemovedAt; the row is never deleted,
    // so the history is kept (R7). Moving to another version adds a new row.
    public class ProjectFramework
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }

        // Stored next to the version, so "at most one version per framework" (R3) is easy to check
        public Guid FrameworkId { get; private set; }
        public Guid FrameworkVersionId { get; private set; }

        public DateTime AddedAt { get; private set; }
        public Guid AddedById { get; private set; }

        // Set when the framework is removed from the project, or the project moves to another version
        public DateTime? RemovedAt { get; private set; }
        public Guid? RemovedById { get; private set; }

        // In use and not yet removed
        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private ProjectFramework() { }

        // Only Project creates these, so its rules always run
        internal ProjectFramework(Guid projectId, Guid frameworkId, Guid frameworkVersionId, Guid addedById, DateTime addedAt)
        {
            ProjectId = projectId;
            FrameworkId = frameworkId;
            FrameworkVersionId = frameworkVersionId;
            AddedById = addedById;
            AddedAt = addedAt;
        }

        internal void Remove(Guid removedById, DateTime removedAt)
        {
            RemovedById = removedById;
            RemovedAt = removedAt;
        }
    }
}
