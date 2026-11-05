using ComplianceTracker.Domain.Common;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Domain.Projects
{
    // The GitHub repository a project reads its user stories from, and how the last sync went (US-018).
    // Stored with the project (an owned type), because it has no meaning without it.
    public partial class GitHubLink
    {
        public const string DefaultLabel = "user-story";

        // "owner/name" as GitHub allows it: letters, digits, '-', '_' and '.'
        [GeneratedRegex(@"^[A-Za-z0-9-]{1,39}/[A-Za-z0-9._-]{1,100}$")]
        private static partial Regex RepositoryFormat();

        private readonly List<SkippedIssue> _skipped = new();

        // e.g. "Zuvuz/compliance-tracker"; for a GitHub Project, the organisation (US-107)
        public string Repository { get; private set; } = "";

        // US-107: an organisation's GitHub Project instead of a repository. The number is null while the user still has
        // to choose one of the organisation's projects.
        public string? ProjectOwner { get; private set; }
        public int? ProjectNumber { get; private set; }
        public string? ProjectTitle { get; private set; }

        public bool IsProject => ProjectOwner is not null;

        // US-107 R3: linked to an organisation, but no project is chosen yet; nothing is read until it is
        public bool NeedsProjectChoice => IsProject && ProjectNumber is null;

        // e.g. "github.com/orgs/acme/projects/3"
        public string? ProjectAddress => IsProject && ProjectNumber is { } number ? $"github.com/orgs/{ProjectOwner}/projects/{number}" : null;

        // Only issues with this label are read (R3)
        public string Label { get; private set; } = DefaultLabel;

        public DateTime ConnectedAt { get; private set; }
        public Guid ConnectedById { get; private set; }

        // R5: when the last sync that reached GitHub finished; null until the first sync
        public DateTime? LastSyncedAt { get; private set; }

        // Why the last sync failed, e.g. "Repository not found or private"; null when it worked
        public string? LastSyncError { get; private set; }

        // R3: issues with the label that were not read in the last sync, and why
        public IReadOnlyList<SkippedIssue> Skipped => _skipped;

        // US-031: a read-only token for a private repository, already encrypted (ISecretProtector).
        // The domain never sees the token itself. Null for public repositories.
        public string? EncryptedToken { get; private set; }
        public DateTime? TokenChangedAt { get; private set; }

        public bool HasToken => EncryptedToken is not null;

        // For EF Core only
        private GitHubLink() { }

        internal GitHubLink(string repository, string? label, Guid connectedById, DateTime at, GitHubLink? previous = null)
        {
            // US-031: the token stays when only the label changes; a new repository needs its own token
            if (previous is not null && string.Equals(previous.Repository, CleanRepository(repository), StringComparison.OrdinalIgnoreCase))
            {
                EncryptedToken = previous.EncryptedToken;
                TokenChangedAt = previous.TokenChangedAt;
            }

            Repository = CleanRepository(repository);
            Label = string.IsNullOrWhiteSpace(label) ? DefaultLabel : label.Trim();
            ConnectedById = connectedById;
            ConnectedAt = at;
        }

        // US-107: an organisation's GitHub Project, or the organisation while a project is still to be chosen.
        // The token stays when the organisation is the same; another organisation needs its own token.
        internal GitHubLink(string owner, int? projectNumber, string? projectTitle, Guid connectedById, DateTime at, GitHubLink? previous)
        {
            if (previous is not null && string.Equals(previous.ProjectOwner ?? previous.Repository.Split('/')[0], owner, StringComparison.OrdinalIgnoreCase))
            {
                EncryptedToken = previous.EncryptedToken;
                TokenChangedAt = previous.TokenChangedAt;
            }

            Repository = owner;
            ProjectOwner = owner;
            ProjectNumber = projectNumber;
            ProjectTitle = projectTitle;
            Label = DefaultLabel;
            ConnectedById = connectedById;
            ConnectedAt = at;
        }

        // US-107 R3: the user chose one of the organisation's projects
        internal void ChooseProject(int number, string title)
        {
            if (!IsProject)
            {
                throw new DomainException("The project is linked to a repository, not an organisation");
            }

            ProjectNumber = number;
            ProjectTitle = title;
        }

        // R1: "owner/name", with or without "https://github.com/" in front
        public static string CleanRepository(string repository)
        {
            var value = (repository ?? "").Trim();
            const string prefix = "https://github.com/";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].TrimEnd('/');
            }

            if (!RepositoryFormat().IsMatch(value))
            {
                throw new DomainException("Enter the repository as owner/name, e.g. Zuvuz/compliance-tracker");
            }

            return value;
        }

        // US-031 R1, R3: sets or replaces the token. The caller has checked it with GitHub and encrypted it.
        public void SetToken(string encryptedToken, DateTime at)
        {
            if (string.IsNullOrWhiteSpace(encryptedToken))
            {
                throw new DomainException("Enter a token");
            }

            EncryptedToken = encryptedToken;
            TokenChangedAt = at;
        }

        // US-031 R3: back to reading the repository as public. Returns false if there was no token.
        public bool RemoveToken(DateTime at)
        {
            if (EncryptedToken is null)
            {
                return false;
            }

            EncryptedToken = null;
            TokenChangedAt = at;
            return true;
        }

        public void RecordSuccess(DateTime at, IEnumerable<SkippedIssue> skipped)
        {
            LastSyncedAt = at;
            LastSyncError = null;
            _skipped.Clear();
            _skipped.AddRange(skipped);
        }

        // The work items and the last good sync time are kept, so the page still shows what was read before
        public void RecordFailure(string message)
        {
            LastSyncError = message;
        }
    }

    // An issue with the label that does not follow the BDD template (R3)
    public record SkippedIssue(string ExternalReference, string Title, string Reason);
}
