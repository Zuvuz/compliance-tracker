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

        // e.g. "Zuvuz/compliance-tracker"
        public string Repository { get; private set; } = "";

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

        // For EF Core only
        private GitHubLink() { }

        internal GitHubLink(string repository, string? label, Guid connectedById, DateTime at)
        {
            Repository = CleanRepository(repository);
            Label = string.IsNullOrWhiteSpace(label) ? DefaultLabel : label.Trim();
            ConnectedById = connectedById;
            ConnectedAt = at;
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
