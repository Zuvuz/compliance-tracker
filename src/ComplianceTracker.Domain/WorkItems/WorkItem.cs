using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.WorkItems
{
    // A user story (or later another kind of work item) read from the team's own tool, e.g. a GitHub issue (US-018).
    // The tracker only keeps a copy and a link; the team keeps writing the story in its own tool (B01, B12).
    // US-032: or a requirement from an uploaded requirement specification (RequirementDocument, B35).
    // Its own aggregate, not part of Project, so a project page does not load every work item to show the project.
    public class WorkItem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }

        public WorkItemSource Source { get; private set; }
        public WorkItemType Type { get; private set; }

        // Where it lives in the source, unique per project and source, e.g. "Zuvuz/compliance-tracker#19".
        // For a document: the requirement's own ID, e.g. "REQ-012", unique per document.
        public string ExternalReference { get; private set; } = "";

        // A link to the item in the source, e.g. https://github.com/Zuvuz/compliance-tracker/issues/19. Empty for a document.
        public string Url { get; private set; } = "";

        public string Title { get; private set; } = "";

        // The acceptance criteria from the BDD template (R4)
        public string Rules { get; private set; } = "";
        public string Examples { get; private set; } = "";
        public string Gherkin { get; private set; } = "";

        // US-032: a requirement's description and acceptance criteria from the document
        public string Description { get; private set; } = "";
        public string AcceptanceCriteria { get; private set; } = "";

        // US-034: a use case's sections; null for every other kind of work item
        public UseCaseSections? UseCase { get; private set; }

        // The labels in the source, comma separated, e.g. "user-story, framework:iso27001"
        public string Labels { get; private set; } = "";

        public WorkItemState State { get; private set; }

        // US-032: the requirement specification it comes from, the newest version that has it,
        // and the version it was no longer in (R5). Null for GitHub.
        public Guid? DocumentId { get; private set; }
        public int? DocumentVersion { get; private set; }
        public int? RemovedInVersion { get; private set; }

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

        // US-032 R3: a requirement read from version `version` of a requirement specification
        public static WorkItem FromDocument(Guid projectId, Guid documentId, WorkItemType type, DocumentWorkItem item, int version, DateTime at)
        {
            if (string.IsNullOrWhiteSpace(item.Reference))
            {
                throw new DomainException("A requirement needs an ID");
            }

            var workItem = new WorkItem
            {
                ProjectId = projectId,
                Source = WorkItemSource.Document,
                Type = type,
                DocumentId = documentId,
                ExternalReference = item.Reference.Trim(),
                FirstSyncedAt = at
            };
            workItem.UpdateFromDocument(item, version, at);
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

        // US-032 R5: the requirement as a new version of the document has it. Returns true if its text changed,
        // or if it was removed in an earlier version and is back.
        public bool UpdateFromDocument(DocumentWorkItem item, int version, DateTime at)
        {
            var title = item.Title.Trim();
            var description = item.Description.Trim();
            var criteria = item.AcceptanceCriteria.Trim();
            var changed = Title != title || Description != description || AcceptanceCriteria != criteria
                || State != WorkItemState.Open || !(UseCase is null ? item.UseCase is null : UseCase.SameAs(item.UseCase));

            Title = title;
            Description = description;
            AcceptanceCriteria = criteria;
            UseCase = item.UseCase; // US-034
            State = WorkItemState.Open;
            DocumentVersion = version;
            RemovedInVersion = null;
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

        // US-032 R5: a new version of the document no longer has it. Kept and marked "Removed in version N".
        public bool MarkRemovedIn(int version)
        {
            if (!MarkRemoved())
            {
                return false;
            }

            RemovedInVersion = version;
            return true;
        }

        // What the AI reads about it (US-020 R3): the title and the text that says what it must do
        public string MatchingText
            => string.Join("\n", new[] { Title, Rules, Examples, Description, AcceptanceCriteria }.Where(t => !string.IsNullOrWhiteSpace(t)));
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

    // US-032: one requirement as a requirement specification has it, e.g. ("REQ-012", "Log in", "...", "...").
    // US-034: or a use case with its sections; its acceptance criteria are the steps and postconditions (R5).
    public record DocumentWorkItem(string Reference, string Title, string Description = "", string AcceptanceCriteria = "",
        UseCaseSections? UseCase = null);

    // Where a work item comes from. Stored as text, so new sources (Azure DevOps, documents) never shift data.
    public enum WorkItemSource
    {
        GitHub,

        // US-032: an uploaded requirement specification
        Document
    }

    // Decision B02: the development method is not modelled, but the kind of work item is
    public enum WorkItemType
    {
        UserStory,

        // US-032: a requirement from a requirement specification
        Requirement,

        // US-034: a Unified Process use case from a use case document
        UseCase
    }

    public enum WorkItemState
    {
        // Open in the source; for a document: active
        Open,

        // Closed in the source (R8)
        Closed,

        // No longer in the source (R8)
        Removed
    }
}
