using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.WorkItems
{
    // One external system the tracker reads work items from, e.g. GitHub (adapter pattern, decision B11).
    // The adapter turns the system's own format into ExternalWorkItem, so the rest of the tracker
    // does not care where a work item comes from. Adapters only read (decision B12).
    public interface IWorkItemSource
    {
        WorkItemSource Source { get; }

        // Every work item with the label, plus those with the label that could not be read and why (R3).
        // Throws WorkItemSourceException with a message for the user when the system cannot be read.
        Task<WorkItemReadResult> ReadAsync(WorkItemQuery query, CancellationToken cancellationToken = default);
    }

    // Where to read: for GitHub the repository ("owner/name") and the label.
    // Token: the decrypted read-only token for a private repository (US-031), or null. Never logged or shown.
    // US-107: with ProjectNumber, Location is the organisation and only that GitHub Project's issues are read; the label
    // is not used, as the project itself decides what belongs to it.
    public record WorkItemQuery(string Location, string Label, string? Token = null, int? ProjectNumber = null)
    {
        // A record prints all its values; this one must never print the token
        public override string ToString()
            => ProjectNumber is { } number ? $"{Location} project {number}" : $"{Location} (label {Label})";
    }

    // US-107 R3: an organisation's GitHub Projects (implemented by the GitHub adapter, read-only)
    public interface IGitHubProjects
    {
        // Throws WorkItemSourceException with a message for the user when they cannot be read
        Task<IReadOnlyList<GitHubProjectInfo>> ListAsync(string organization, string token, CancellationToken cancellationToken = default);
    }

    public record GitHubProjectInfo(int Number, string Title);

    public record WorkItemReadResult(IReadOnlyList<ExternalWorkItem> Items, IReadOnlyList<SkippedIssue> Skipped);

    // The source could not be read, e.g. "Repository not found or private". The message is shown to the user.
    public class WorkItemSourceException : Exception
    {
        public WorkItemSourceException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    // Storage for work items (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IWorkItemRepository
    {
        // Every work item of the project from one source, also closed and removed ones
        Task<IReadOnlyList<WorkItem>> ListAsync(Guid projectId, WorkItemSource source, CancellationToken cancellationToken = default);

        // Every work item of the project, newest first
        Task<IReadOnlyList<WorkItem>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        // US-032: every requirement of one requirement specification, also removed ones
        Task<IReadOnlyList<WorkItem>> ListByDocumentAsync(Guid projectId, Guid documentId, CancellationToken cancellationToken = default);

        Task AddAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    }

    // Tells open project pages that their work items changed (R7). Implemented in Web with SignalR.
    public interface IWorkItemNotifier
    {
        Task WorkItemsChangedAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
