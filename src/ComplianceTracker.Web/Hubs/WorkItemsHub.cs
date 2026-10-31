using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.SignalR;

namespace ComplianceTracker.Web.Hubs
{
    // US-018 R7: an open project page listens here and reloads its work items when a sync finds changes.
    // Requires sign-in like every other endpoint (FallbackPolicy in Program.cs).
    public class WorkItemsHub : Hub
    {
        public const string Path = "/hubs/work-items";
        public const string ChangedMessage = "workItemsChanged";

        private readonly WorkItemQueries _workItems;
        private readonly CurrentPrincipal _principal;

        public WorkItemsHub(WorkItemQueries workItems, CurrentPrincipal principal)
        {
            _workItems = workItems;
            _principal = principal;
        }

        public static string GroupFor(Guid projectId) => $"project-{projectId}";

        // The page asks to follow one project. R9: only members of the project's organisation may.
        public async Task Follow(Guid projectId)
        {
            // Inside a hub the user comes from the connection, not from IHttpContextAccessor
            _principal.User = Context.User;

            if (!await _workItems.CanSeeAsync(projectId, Context.ConnectionAborted))
            {
                throw new HubException("Project not found");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(projectId), Context.ConnectionAborted);
        }
    }

    // IWorkItemNotifier with SignalR: tells every page following the project
    public class SignalRWorkItemNotifier : IWorkItemNotifier
    {
        private readonly IHubContext<WorkItemsHub> _hub;

        public SignalRWorkItemNotifier(IHubContext<WorkItemsHub> hub)
        {
            _hub = hub;
        }

        public Task WorkItemsChangedAsync(Guid projectId, CancellationToken cancellationToken = default)
            => _hub.Clients.Group(WorkItemsHub.GroupFor(projectId)).SendAsync(WorkItemsHub.ChangedMessage, projectId, cancellationToken);
    }
}
