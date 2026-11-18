using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.WorkItems
{
    // US-030: user stories from Azure Boards (dev.azure.com), read-only, with the project's encrypted token.
    // The sync is the same as for GitHub (US-018): add, update and mark, never delete.
    public partial class WorkItemSyncService
    {
        public const string AzureNotSetUp = "Reading from Azure DevOps is not set up in this tracker";

        // R1–R3, R10: checks the token with Azure DevOps, then links and stores it encrypted. Nothing is saved if Azure refuses.
        // Logged as AzureProjectConnected and AccessTokenChanged, never with the token.
        public async Task LinkAzureDevOpsAsync(
            Guid projectId, string? organization, string? azureProject, string? areaPath, string? token, CancellationToken cancellationToken = default)
        {
            var (memberOrganization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(memberOrganization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            var cleanOrganization = AzureDevOpsLink.CleanOrganization(organization ?? "");
            var cleanProject = AzureDevOpsLink.CleanProject(azureProject ?? "");

            // R3: a new token, or the one already stored for the same organisation and project
            var value = (token ?? "").Trim();
            if (value.Length == 0)
            {
                value = project.AzureDevOps is { } current
                        && string.Equals(current.Organization, cleanOrganization, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(current.Project, cleanProject, StringComparison.OrdinalIgnoreCase)
                    ? AzureToken(current)
                    : throw new DomainException("Enter a read-only Personal Access Token (Work Items: Read)");
            }

            await CheckAzureTokenAsync(cleanOrganization, cleanProject, areaPath, value, cancellationToken);

            project.LinkAzureDevOps(cleanOrganization, cleanProject, areaPath, _secrets.Protect(value), user.Id, Now);
            var link = project.AzureDevOps!;
            await _log.LogAsync(ActivityAction.AzureProjectConnected, project.Id,
                link.AreaPath is null ? link.Address : $"{link.Address} (area {link.AreaPath})", cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                await _log.LogAsync(ActivityAction.AccessTokenChanged, project.Id, "Azure DevOps token set", cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R3: removes the link and its token; the work items read before are kept
        public async Task UnlinkAzureDevOpsAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.UnlinkAzureDevOps())
            {
                await _log.LogAsync(ActivityAction.AccessTokenChanged, project.Id, "Azure DevOps link and token removed", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task CheckAzureTokenAsync(string organization, string azureProject, string? areaPath, string token, CancellationToken cancellationToken)
        {
            var source = _azure ?? throw new DomainException(AzureNotSetUp);
            try
            {
                await source.ReadAsync(new WorkItemQuery($"{organization}/{azureProject}", "", token, AreaPath: areaPath), cancellationToken);
            }
            catch (WorkItemSourceException error)
            {
                throw new DomainException(error.Message);
            }
        }

        // R7, R8, R10: reads, then adds, updates and marks; a refused token keeps the work items and shows why
        private async Task<SyncResult> SyncAzureAsync(Project project, Guid? userId, CancellationToken cancellationToken)
        {
            var link = project.AzureDevOps!;
            var now = Now;
            WorkItemReadResult read;
            try
            {
                var source = _azure ?? throw new WorkItemSourceException(AzureNotSetUp);
                read = await source.ReadAsync(new WorkItemQuery(link.Address, "", AzureToken(link), AreaPath: link.AreaPath), cancellationToken);
            }
            catch (WorkItemSourceException error)
            {
                link.RecordFailure(error.Message);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return SyncResult.Failed(error.Message);
            }

            var result = await ApplyAsync(project, WorkItemSource.AzureDevOps, read, now, cancellationToken);
            link.RecordSuccess(now, read.Skipped);
            if (userId is not null)
            {
                await _log.LogAsync(ActivityAction.WorkItemsSynced, project.Id, $"Azure DevOps: {result}", cancellationToken);
            }
            else if (result.HasChanges)
            {
                await _log.LogSystemAsync(project.OrganizationId, ActivityAction.WorkItemsSynced, project.Id, $"Scheduled, Azure DevOps: {result}", cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (result.HasChanges)
            {
                await _notifier.WorkItemsChangedAsync(project.Id, cancellationToken);
            }

            return result;
        }

        // The decrypted token, only for the request to Azure DevOps
        private string AzureToken(AzureDevOpsLink link)
        {
            try
            {
                return _secrets.Unprotect(link.EncryptedToken);
            }
            catch (SecretUnreadableException error)
            {
                throw new WorkItemSourceException("The stored token can no longer be read. Enter it again", error);
            }
        }
    }
}
