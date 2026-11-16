using System.Text.RegularExpressions;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Projects
{
    // US-030: the Azure DevOps project (dev.azure.com) the project reads its user stories from, read-only (B141).
    // Stored with the project like the GitHub link; its skipped items are replaced at every sync.
    public partial class AzureDevOpsLink
    {
        public const int MaxAreaPathLength = 400;

        // Azure DevOps names: an organisation is letters, digits and '-'; a project may also have spaces, '.' and '_'
        [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,49}$")]
        private static partial Regex OrganizationFormat();

        [GeneratedRegex(@"^[^/\\:*?""<>|;#$%&+=,@\[\]{}^`~]{1,64}$")]
        private static partial Regex ProjectFormat();

        private readonly List<SkippedIssue> _skipped = new();

        // R1, e.g. "acme", "Portal" and "Portal\Web"
        public string Organization { get; private set; } = "";
        public string Project { get; private set; } = "";
        public string? AreaPath { get; private set; }

        // e.g. "acme/Portal"
        public string Address => $"{Organization}/{Project}";

        public DateTime ConnectedAt { get; private set; }
        public Guid ConnectedById { get; private set; }

        // R2, R3: the read-only token, already encrypted (ISecretProtector); the domain never sees the token itself
        public string EncryptedToken { get; private set; } = "";
        public DateTime TokenChangedAt { get; private set; }

        public DateTime? LastSyncedAt { get; private set; }

        // R8: why the last sync failed, e.g. "The token was refused by Azure DevOps"; null when it worked
        public string? LastSyncError { get; private set; }

        // R4: the user stories without acceptance criteria in the last sync
        public IReadOnlyList<SkippedIssue> Skipped => _skipped;

        // For EF Core only
        private AzureDevOpsLink() { }

        internal AzureDevOpsLink(string organization, string project, string? areaPath, string encryptedToken, Guid connectedById, DateTime at)
        {
            Organization = CleanOrganization(organization);
            Project = CleanProject(project);
            AreaPath = string.IsNullOrWhiteSpace(areaPath) ? null : areaPath.Trim();
            if (AreaPath?.Length > MaxAreaPathLength)
            {
                throw new DomainException($"The area path must be {MaxAreaPathLength} characters or fewer");
            }

            SetToken(encryptedToken, at);
            ConnectedById = connectedById;
            ConnectedAt = at;
        }

        // R1: the organisation as in dev.azure.com/{organisation}, with or without the address in front
        public static string CleanOrganization(string organization)
        {
            var value = (organization ?? "").Trim().TrimEnd('/');
            foreach (var prefix in new[] { "https://dev.azure.com/", "dev.azure.com/" })
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value[prefix.Length..];
                }
            }

            if (!OrganizationFormat().IsMatch(value))
            {
                throw new DomainException("Enter the Azure DevOps organisation as in dev.azure.com/{organisation}");
            }

            return value;
        }

        public static string CleanProject(string project)
        {
            var value = (project ?? "").Trim();
            if (!ProjectFormat().IsMatch(value) || value.StartsWith('.') || value.EndsWith('.'))
            {
                throw new DomainException("Enter the Azure DevOps project's name");
            }

            return value;
        }

        // R3: sets or replaces the token; the caller has checked it with Azure DevOps and encrypted it
        public void SetToken(string encryptedToken, DateTime at)
        {
            if (string.IsNullOrWhiteSpace(encryptedToken))
            {
                throw new DomainException("Enter a token");
            }

            EncryptedToken = encryptedToken;
            TokenChangedAt = at;
        }

        public void RecordSuccess(DateTime at, IEnumerable<SkippedIssue> skipped)
        {
            LastSyncedAt = at;
            LastSyncError = null;
            _skipped.Clear();
            _skipped.AddRange(skipped);
        }

        // R8: the work items and the last good sync time are kept
        public void RecordFailure(string message) => LastSyncError = message.Length <= 500 ? message : message[..500];

        // R5: the link to a work item, e.g. https://dev.azure.com/acme/Portal/_workitems/edit/120
        public static string WorkItemUrl(string organization, string project, int id)
            => $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_workitems/edit/{id}";
    }
}
