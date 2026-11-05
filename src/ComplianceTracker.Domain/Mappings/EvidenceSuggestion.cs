using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-058: evidence the tracker suggests for a requirement of a project, from what already exists: a test run whose
    // passing scenarios are tagged with the requirement (R1, R2), or the pull request that closed a story linked to it (R3).
    // Tags and links decide it, not an AI. Nothing becomes evidence until a person accepts it (R4, AI Act Art. 14 style).
    // A rejected suggestion keeps its reason and the same source is never suggested again (R5).
    public class EvidenceSuggestion
    {
        public const int MaxTitleLength = 200;
        public const int MaxScenariosLength = 4000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid RequirementId { get; private set; }

        public EvidenceSourceKind Source { get; private set; }

        // e.g. "Tests passed: 3 scenarios (test run 03-11-2026)" or "PR 42 – Keycloak sign-in"
        public string Title { get; private set; } = "";

        // The pull request's address; null for a test run, which becomes a file
        public string? Url { get; private set; }

        // What makes the source unique: the test results' SHA-256, or e.g. "acme/portal#42" (R5)
        public string ExternalId { get; private set; } = "";

        // R2: the run's passing and failing scenarios with the requirement's tag, one per line
        public int PassingCount { get; private set; }
        public int FailingCount { get; private set; }
        public string? PassingScenarios { get; private set; }
        public string? FailingScenarios { get; private set; }

        // R9: the uploaded file's name; its SHA-256 is the ExternalId
        public string? ResultsFileName { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public EvidenceSuggestionStatus Status { get; private set; }
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public string? RejectionReason { get; private set; }

        // The evidence an accepted suggestion became
        public Guid? EvidenceId { get; private set; }

        public bool IsPending => Status == EvidenceSuggestionStatus.Pending;

        // For EF Core only
        private EvidenceSuggestion() { }

        // R1, R2: one per requirement and test run. A failing scenario with the same tag blocks it: it is shown, never accepted.
        public static EvidenceSuggestion FromTestRun(
            Guid projectId, Guid requirementId, string resultsSha256, string resultsFileName,
            IReadOnlyCollection<string> passing, IReadOnlyCollection<string> failing, DateTime at)
            => new()
            {
                ProjectId = projectId,
                RequirementId = requirementId,
                Source = EvidenceSourceKind.TestRun,
                Title = Cut(failing.Count > 0
                    ? $"Tests failed: {failing.Count} of {passing.Count + failing.Count} scenarios (test run {at:dd-MM-yyyy})"
                    : $"Tests passed: {passing.Count} {(passing.Count == 1 ? "scenario" : "scenarios")} (test run {at:dd-MM-yyyy})", MaxTitleLength),
                ExternalId = resultsSha256,
                ResultsFileName = Cut(resultsFileName, 200),
                PassingCount = passing.Count,
                FailingCount = failing.Count,
                PassingScenarios = Lines(passing),
                FailingScenarios = Lines(failing),
                CreatedAt = at,
                Status = failing.Count > 0 ? EvidenceSuggestionStatus.Blocked : EvidenceSuggestionStatus.Pending
            };

        // R3: e.g. "PR 42 – Keycloak sign-in". The story's reference is part of the id ("acme/portal#4>acme/portal#42"),
        // so the next sync knows which stories were already asked about.
        public static EvidenceSuggestion FromPullRequest(
            Guid projectId, Guid requirementId, string storyReference, string repository, int number, string title, string url, DateTime at)
            => new()
            {
                ProjectId = projectId,
                RequirementId = requirementId,
                Source = EvidenceSourceKind.PullRequest,
                Title = Cut($"PR {number} – {title}", MaxTitleLength),
                Url = url,
                ExternalId = $"{storyReference}>{repository}#{number}",
                CreatedAt = at,
                Status = EvidenceSuggestionStatus.Pending
            };

        // R4: the caller creates the evidence and its link
        public void Accept(Guid evidenceId, Guid userId, UserRole? role, DateTime at)
        {
            EnsurePending();
            Status = EvidenceSuggestionStatus.Accepted;
            EvidenceId = evidenceId;
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // R5: a reason is needed
        public void Reject(string? reason, Guid userId, UserRole? role, DateTime at)
        {
            EnsurePending();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Give a reason for rejecting the suggestion");
            }

            Status = EvidenceSuggestionStatus.Rejected;
            RejectionReason = Cut(reason.Trim(), 1000);
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        // The requirement was marked not applicable after the suggestion was made (R6)
        public void Withdraw()
        {
            if (Status is EvidenceSuggestionStatus.Pending or EvidenceSuggestionStatus.Blocked)
            {
                Status = EvidenceSuggestionStatus.Withdrawn;
            }
        }

        private void EnsurePending()
        {
            if (!IsPending)
            {
                throw new DomainException(Status == EvidenceSuggestionStatus.Blocked
                    ? "A suggestion with failing tests cannot be accepted"
                    : "The suggestion has already been decided");
            }
        }

        private static string? Lines(IReadOnlyCollection<string> names)
            => names.Count == 0 ? null : Cut(string.Join("\n", names), MaxScenariosLength);

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }

    // Stored as text
    public enum EvidenceSourceKind
    {
        TestRun,
        PullRequest
    }

    public enum EvidenceSuggestionStatus
    {
        Pending,

        // R2: a failing scenario with the same tag; shown, never accepted
        Blocked,
        Accepted,
        Rejected,

        // The requirement no longer applies (R6)
        Withdrawn
    }
}
