using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.WorkItems
{
    // A user story (or later another kind of work item) read from the team's own tool, e.g. a GitHub issue (US-018).
    // The tracker only keeps a copy and a link; the team keeps writing the story in its own tool (B01, B12).
    // Its own aggregate, not part of Project, so a project page does not load every work item to show the project.
    public class WorkItem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }

        public WorkItemSource Source { get; private set; }
        public WorkItemType Type { get; private set; }

        // Where it lives in the source, unique per project and source, e.g. "Zuvuz/compliance-tracker#19"
        public string ExternalReference { get; private set; } = "";

        // A link to the item in the source, e.g. https://github.com/Zuvuz/compliance-tracker/issues/19
        public string Url { get; private set; } = "";

        public string Title { get; private set; } = "";

        // The acceptance criteria from the BDD template (R4)
        public string Rules { get; private set; } = "";
        public string Examples { get; private set; } = "";
        public string Gherkin { get; private set; } = "";

        // The labels in the source, comma separated, e.g. "user-story, framework:iso27001"
        public string Labels { get; private set; } = "";

        public WorkItemState State { get; private set; }

        // When it was first read and when the source last had it (UTC)
        public DateTime FirstSyncedAt { get; private set; }
        public DateTime LastSyncedAt { get; private set; }

        // For EF Core only
        private WorkItem() { }

        public static WorkItem Create(Guid projectId, WorkItemSource source, ExternalWorkItem item, DateTime at)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalReference))
            {
                throw new DomainException("A work item needs a reference to its source");
            }

            var workItem = new WorkItem
            {
                ProjectId = projectId,
                Source = source,
                Type = WorkItemType.UserStory,
                ExternalReference = item.ExternalReference,
                FirstSyncedAt = at
            };
            workItem.Update(item, at);
            return workItem;
        }

        // R4, R8: takes the source's current version. Returns true if anything the user can see changed.
        public bool Update(ExternalWorkItem item, DateTime at)
        {
            var state = item.IsClosed ? WorkItemState.Closed : WorkItemState.Open;
            var changed = Title != item.Title || Rules != item.Rules || Examples != item.Examples
                || Gherkin != item.Gherkin || Labels != item.Labels || Url != item.Url || State != state;

            Title = item.Title;
            Rules = item.Rules;
            Examples = item.Examples;
            Gherkin = item.Gherkin;
            Labels = item.Labels;
            Url = item.Url;
            State = state;
            LastSyncedAt = at;
            return changed;
        }

        // R8: the source no longer returns it (label removed, issue deleted, or the project moved to another
        // repository). It is kept and marked, never deleted. Returns true if it was not already marked.
        public bool MarkRemoved()
        {
            if (State == WorkItemState.Removed)
            {
                return false;
            }

            State = WorkItemState.Removed;
            return true;
        }
    }

    // A work item as the source has it right now, already turned into the tracker's terms by the adapter (B11)
    public record ExternalWorkItem(
        string ExternalReference,
        string Url,
        string Title,
        string Rules,
        string Examples,
        string Gherkin,
        string Labels,
        bool IsClosed);

    // Where a work item comes from. Stored as text, so new sources (Azure DevOps, documents) never shift data.
    public enum WorkItemSource
    {
        GitHub
    }

    // Decision B02: the development method is not modelled, but the kind of work item is
    public enum WorkItemType
    {
        UserStory
    }

    public enum WorkItemState
    {
        Open,

        // Closed in the source (R8)
        Closed,

        // No longer in the source (R8)
        Removed
    }
}
