using ComplianceTracker.Domain.Users;
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
        private readonly IGitHubProjects _githubProjects;
        private readonly Compliance.EvidenceSuggestionService _evidenceSuggestions;

        private readonly Ai.StoryDraftLinker _drafts; // US-106 R5

        public WorkItemSyncService(
            IProjectRepository projects,
            IWorkItemRepository workItems,
            IEnumerable<IWorkItemSource> sources,
            IWorkItemNotifier notifier,
            ISecretProtector secrets,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock,
            Compliance.EvidenceSuggestionService evidenceSuggestions,
            IGitHubProjects githubProjects,
            Ai.StoryDraftLinker drafts)
        {
            _drafts = drafts;
            _githubProjects = githubProjects;
            _evidenceSuggestions = evidenceSuggestions;
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

        // US-107: the address may be a repository (as before), an organisation's GitHub Project, or an organisation, whose
        // projects are then listed to choose one. A GitHub Project can only be read with a token (R6), given here.
        public async Task LinkGitHubAddressAsync(
            Guid projectId, string address, string? label, string? token, CancellationToken cancellationToken = default)
        {
            var parsed = GitHubAddress.Parse(address);
            if (parsed.Kind == GitHubAddressKind.Repository)
            {
                await LinkGitHubAsync(projectId, $"{parsed.Owner}/{parsed.Repository}", label, cancellationToken);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    await SetGitHubTokenAsync(projectId, token, cancellationToken);
                }

                return;
            }

            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");

            // R6: a new token, or the one already stored for the same organisation
            var value = string.IsNullOrWhiteSpace(token)
                ? project.GitHub is { } current && string.Equals(current.ProjectOwner ?? current.Repository.Split('/')[0], parsed.Owner, StringComparison.OrdinalIgnoreCase) ? Token(current) : null
                : token.Trim();
            if (value is null)
            {
                throw new DomainException("Reading a GitHub Project needs a token with read access to projects");
            }

            var projects = await ListProjectsAsync(parsed.Owner, value, cancellationToken);
            GitHubProjectInfo? chosen = null;
            if (parsed.ProjectNumber is { } number)
            {
                chosen = projects.FirstOrDefault(p => p.Number == number)
                    ?? throw new DomainException("GitHub Project not found, or the token cannot see it");
                await EnsureNotLinkedElsewhereAsync(organization.Id, project, parsed.Owner, number, cancellationToken);
            }

            if (project.LinkGitHubProject(parsed.Owner, chosen?.Number, chosen?.Title, user.Id, Now) || !string.IsNullOrWhiteSpace(token))
            {
                project.GitHub!.SetToken(_secrets.Protect(value), Now);
                await _log.LogAsync(ActivityAction.RepositoryConnected, project.Id, chosen is null
                    ? $"GitHub organisation {parsed.Owner} (a project is still to be chosen)"
                    : $"GitHub Project {project.GitHub.ProjectAddress} ({chosen.Title})", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (chosen is not null)
            {
                await SyncAsync(project, user.Id, cancellationToken);
            }
        }

        // US-107 R3: the organisation's projects, while one is still to be chosen; empty otherwise
        public async Task<IReadOnlyList<GitHubProjectInfo>> ProjectsToChooseAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken);
            if (project?.GitHub is not { NeedsProjectChoice: true } link || Token(link) is not { } token)
            {
                return [];
            }

            try
            {
                return await _githubProjects.ListAsync(link.ProjectOwner!, token, cancellationToken);
            }
            catch (WorkItemSourceException)
            {
                return [];
            }
        }

        // US-107 R3, R5
        public async Task ChooseGitHubProjectAsync(Guid projectId, int number, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            var link = project.GitHub is { IsProject: true } l ? l : throw new DomainException("Link the project to a GitHub organisation first");
            var token = Token(link) ?? throw new DomainException("Reading a GitHub Project needs a token with read access to projects");

            var chosen = (await ListProjectsAsync(link.ProjectOwner!, token, cancellationToken)).FirstOrDefault(p => p.Number == number)
                ?? throw new DomainException("GitHub Project not found, or the token cannot see it");
            await EnsureNotLinkedElsewhereAsync(organization.Id, project, link.ProjectOwner!, number, cancellationToken);

            project.ChooseGitHubProject(chosen.Number, chosen.Title);
            await _log.LogAsync(ActivityAction.RepositoryConnected, project.Id, $"GitHub Project {link.ProjectAddress} ({chosen.Title})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SyncAsync(project, user.Id, cancellationToken);
        }

        private async Task<IReadOnlyList<GitHubProjectInfo>> ListProjectsAsync(string owner, string token, CancellationToken cancellationToken)
        {
            try
            {
                return await _githubProjects.ListAsync(owner, token, cancellationToken);
            }
            catch (WorkItemSourceException error)
            {
                throw new DomainException(error.Message);
            }
        }

        // R5: a GitHub Project belongs to one tracker project of the organisation, so stories are never shared
        private async Task EnsureNotLinkedElsewhereAsync(Guid organizationId, Project project, string owner, int number, CancellationToken cancellationToken)
        {
            var other = (await _projects.ListAsync(organizationId, cancellationToken))
                .FirstOrDefault(p => p.Id != project.Id && p.GitHub is { IsProject: true } g
                    && string.Equals(g.ProjectOwner, owner, StringComparison.OrdinalIgnoreCase) && g.ProjectNumber == number);
            if (other is not null)
            {
                throw new DomainException($"Already linked to {other.Name}");
            }
        }

        // R1, R10: links the project to a public repository (or another one). Logged with the repository.
        public async Task LinkGitHubAsync(Guid projectId, string repository, string? label, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
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

            // R2: GitHub must accept the token for this repository (US-107: or this organisation's projects); otherwise nothing is saved
            try
            {
                if (link.NeedsProjectChoice)
                {
                    await _githubProjects.ListAsync(link.ProjectOwner!, value, cancellationToken);
                }
                else
                {
                    await _github.ReadAsync(new WorkItemQuery(link.Repository, link.Label, value, link.ProjectNumber), cancellationToken);
                }
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
            var (_, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await LinkedProjectAsync(projectId, cancellationToken, Permission.LinkWork);
            return await SyncAsync(project, user.Id, cancellationToken);
        }

        // The member's own project, which must be linked to GitHub
        private async Task<Project> LinkedProjectAsync(Guid projectId, CancellationToken cancellationToken, Permission permission = Permission.EditProjects)
        {
            var (organization, _) = await _member.RequireAsync(permission, cancellationToken);
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

            // US-107 R3: nothing is read until a project of the organisation is chosen
            if (link.NeedsProjectChoice)
            {
                return SyncResult.Failed("Choose one of the organisation's GitHub Projects first");
            }

            WorkItemReadResult read;
            try
            {
                var token = Token(link);
                if (link.IsProject && token is null)
                {
                    throw new WorkItemSourceException("Reading a GitHub Project needs a token with read access to projects");
                }

                read = await _github.ReadAsync(new WorkItemQuery(link.Repository, link.Label, token, link.ProjectNumber), cancellationToken);
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

            // US-106 R5: an issue made from an accepted AI draft is linked to its requirement, once it is saved
            if (await _drafts.LinkAcceptedAsync(project, cancellationToken) > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // US-058 R3: the pull requests that closed linked stories become evidence suggestions
            if (await _evidenceSuggestions.SuggestFromPullRequestsAsync(project, Token(link), cancellationToken) > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

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
