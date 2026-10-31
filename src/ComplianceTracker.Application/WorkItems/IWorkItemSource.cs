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

    // Where to read: for GitHub the repository ("owner/name") and the label
    public record WorkItemQuery(string Location, string Label);

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

        Task AddAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    }

    // Tells open project pages that their work items changed (R7). Implemented in Web with SignalR.
    public interface IWorkItemNotifier
    {
        Task WorkItemsChangedAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
