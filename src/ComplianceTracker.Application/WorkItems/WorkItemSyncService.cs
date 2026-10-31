using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.WorkItems
{
    // US-018: links a project to a GitHub repository and reads its user stories.
    // US-031: a private repository is read with a read-only token, stored encrypted (ISecretProtector).
    // A sync adds new work items, updates changed ones and marks closed or removed ones; nothing is deleted (R8).
    // It runs when a member clicks "Sync now" (R5) and by itself at a fixed interval (R6, ScheduledSync).
    public class WorkItemSyncService
    {
        private readonly IProjectRepository _projects;
        private readonly IWorkItemRepository _workItems;
        private readonly IWorkItemSource _github;
        private readonly IWorkItemNotifier _notifier;
        private readonly ISecretProtector _secrets;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public WorkItemSyncService(
            IProjectRepository projects,
            IWorkItemRepository workItems,
            IEnumerable<IWorkItemSource> sources,
            IWorkItemNotifier notifier,
            ISecretProtector secrets,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _workItems = workItems;
            _github = sources.Single(s => s.Source == WorkItemSource.GitHub);
            _notifier = notifier;
            _secrets = secrets;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R10: links the project to a public repository (or another one). Logged with the repository.
        public async Task LinkGitHubAsync(Guid projectId, string repository, string? label, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");

            if (project.LinkGitHub(repository, label, user.Id, Now))
            {
                await _log.LogAsync(ActivityAction.RepositoryConnected, project.Id,
                    $"{project.GitHub!.Repository} (label {project.GitHub.Label})", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // US-031 R2, R3, R5: checks the token with GitHub, then stores it encrypted. Logged without the value.
        public async Task SetGitHubTokenAsync(Guid projectId, string token, CancellationToken cancellationToken = default)
        {
            var project = await LinkedProjectAsync(projectId, cancellationToken);
            var link = project.GitHub!;
            var value = (token ?? "").Trim();
            if (value.Length == 0)
            {
                throw new DomainException("Enter a token");
            }

            // R2: GitHub must accept the token for this repository; otherwise nothing is saved
            try
            {
                await _github.ReadAsync(new WorkItemQuery(link.Repository, link.Label, value), cancellationToken);
            }
            catch (WorkItemSourceException error)
            {
                throw new DomainException(error.Message);
            }

            link.SetToken(_secrets.Protect(value), Now);
            await _log.LogAsync(ActivityAction.AccessTokenChanged, project.Id, "GitHub token set", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-031 R3, R5
        public async Task RemoveGitHubTokenAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await LinkedProjectAsync(projectId, cancellationToken);
            if (project.GitHub!.RemoveToken(Now))
            {
                await _log.LogAsync(ActivityAction.AccessTokenChanged, project.Id, "GitHub token removed", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // R5: "Sync now" for the signed-in member's project
        public async Task<SyncResult> SyncNowAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (_, user) = await _member.RequireAsync(cancellationToken);
            var project = await LinkedProjectAsync(projectId, cancellationToken);
            return await SyncAsync(project, user.Id, cancellationToken);
        }

        // The member's own project, which must be linked to GitHub
        private async Task<Project> LinkedProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");

            if (project.GitHub is null)
            {
                throw new DomainException("Link the project to a GitHub repository first");
            }

            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return project;
        }

        // R6: the scheduled check. Reads every linked project of every organisation, without a user.
        // One project failing does not stop the others.
        public async Task SyncAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (var projectId in await _projects.ListIdsLinkedToGitHubAsync(cancellationToken))
            {
                var project = await _projects.GetForSyncAsync(projectId, cancellationToken);
                if (project?.GitHub is not null && !project.IsClosed)
                {
                    await SyncAsync(project, userId: null, cancellationToken);
                }
            }
        }

        private async Task<SyncResult> SyncAsync(Project project, Guid? userId, CancellationToken cancellationToken)
        {
            var link = project.GitHub!;
            var now = Now;

            WorkItemReadResult read;
            try
            {
                read = await _github.ReadAsync(new WorkItemQuery(link.Repository, link.Label, Token(link)), cancellationToken);
            }
            catch (WorkItemSourceException error)
            {
                // The work items from earlier syncs are kept; the page shows why this one failed
                link.RecordFailure(error.Message);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return SyncResult.Failed(error.Message);
            }

            // Add, update and mark (R4, R8). Matching on the reference, e.g. "Zuvuz/compliance-tracker#19".
            var existing = (await _workItems.ListAsync(project.Id, WorkItemSource.GitHub, cancellationToken))
                .ToDictionary(w => w.ExternalReference, StringComparer.OrdinalIgnoreCase);
            var result = new SyncResult(Skipped: read.Skipped.Count);

            foreach (var item in read.Items)
            {
                if (existing.Remove(item.ExternalReference, out var workItem))
                {
                    var wasClosed = workItem.State == WorkItemState.Closed;
                    if (workItem.Update(item, now))
                    {
                        result = item.IsClosed && !wasClosed ? result with { Closed = result.Closed + 1 } : result with { Changed = result.Changed + 1 };
                    }
                }
                else
                {
                    await _workItems.AddAsync(WorkItem.Create(project.Id, WorkItemSource.GitHub, item, now), cancellationToken);
                    result = result with { Added = result.Added + 1 };
                }
            }

            foreach (var gone in existing.Values)
            {
                if (gone.MarkRemoved())
                {
                    result = result with { Removed = result.Removed + 1 };
                }
            }

            link.RecordSuccess(now, read.Skipped);

            // R10: a member's sync is always logged; a scheduled one only when it found changes, so the log is not flooded
            if (userId is not null)
            {
                await _log.LogAsync(ActivityAction.WorkItemsSynced, project.Id, result.ToString(), cancellationToken);
            }
            else if (result.HasChanges)
            {
                await _log.LogSystemAsync(project.OrganizationId, ActivityAction.WorkItemsSynced, project.Id,
                    "Scheduled: " + result, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // R7: open project pages update themselves
            if (result.HasChanges)
            {
                await _notifier.WorkItemsChangedAsync(project.Id, cancellationToken);
            }

            return result;
        }

        // US-031: the decrypted token, only for the request to GitHub
        private string? Token(GitHubLink link)
        {
            if (link.EncryptedToken is null)
            {
                return null;
            }

            try
            {
                return _secrets.Unprotect(link.EncryptedToken);
            }
            catch (SecretUnreadableException error)
            {
                throw new WorkItemSourceException("The stored token can no longer be read. Enter it again", error);
            }
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }

    // What one sync did, e.g. "1 added, 0 changed, 0 closed, 0 removed, 1 skipped"
    public record SyncResult(int Added = 0, int Changed = 0, int Closed = 0, int Removed = 0, int Skipped = 0, string? Error = null)
    {
        public bool HasChanges => Added + Changed + Closed + Removed > 0;

        public static SyncResult Failed(string error) => new(Error: error);

        public override string ToString()
            => Error ?? $"{Added} added, {Changed} changed, {Closed} closed, {Removed} removed, {Skipped} skipped";
    }
}
