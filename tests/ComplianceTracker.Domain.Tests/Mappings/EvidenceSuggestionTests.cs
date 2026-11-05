using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Unit tests of EvidenceSuggestion (US-058): suggestions from test runs and pull requests, decided once by a person.
// Every test follows Arrange / Act / Assert.
public class EvidenceSuggestionTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Requirement = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private static EvidenceSuggestion PullRequest()
        => EvidenceSuggestion.FromPullRequest(Project, Requirement, "acme/portal#4", "acme/portal", 42, "Keycloak sign-in",
            "https://github.com/acme/portal/pull/42", At);

    [Fact]
    public void Passing_tests_make_a_pending_suggestion_with_the_count_in_the_title()
    {
        // Arrange / Act
        var suggestion = EvidenceSuggestion.FromTestRun(Project, Requirement, "abc123", "results.trx", ["A", "B", "C"], [], At);

        // Assert
        Assert.Equal(EvidenceSuggestionStatus.Pending, suggestion.Status);
        Assert.Equal("Tests passed: 3 scenarios (test run 03-11-2026)", suggestion.Title);
        Assert.Equal("A\nB\nC", suggestion.PassingScenarios);
        Assert.Equal("abc123", suggestion.ExternalId);
    }

    [Fact]
    public void A_failing_test_blocks_the_suggestion()
    {
        // Arrange
        var suggestion = EvidenceSuggestion.FromTestRun(Project, Requirement, "abc123", "results.trx", ["A", "B"], ["C"], At);

        // Act / Assert
        Assert.Equal(EvidenceSuggestionStatus.Blocked, suggestion.Status);
        Assert.Equal(1, suggestion.FailingCount);
        Assert.Throws<DomainException>(() => suggestion.Accept(Guid.NewGuid(), Anna, UserRole.Developer, At));
    }

    [Fact]
    public void A_pull_request_suggestion_names_the_story_it_came_from()
    {
        // Arrange / Act
        var suggestion = PullRequest();

        // Assert
        Assert.Equal("PR 42 – Keycloak sign-in", suggestion.Title);
        Assert.Equal("acme/portal#4>acme/portal#42", suggestion.ExternalId);
        Assert.Equal(EvidenceSourceKind.PullRequest, suggestion.Source);
    }

    [Fact]
    public void Accepting_keeps_the_evidence_and_who_decided()
    {
        // Arrange
        var suggestion = PullRequest();
        var evidence = Guid.NewGuid();

        // Act
        suggestion.Accept(evidence, Anna, UserRole.Developer, At);

        // Assert
        Assert.Equal(EvidenceSuggestionStatus.Accepted, suggestion.Status);
        Assert.Equal((evidence, Anna, UserRole.Developer), (suggestion.EvidenceId!.Value, suggestion.DecidedById!.Value, suggestion.DecidedRole!.Value));
        Assert.Throws<DomainException>(() => suggestion.Reject("Too late", Anna, null, At));
    }

    [Fact]
    public void Rejecting_needs_a_reason()
    {
        // Arrange
        var suggestion = PullRequest();

        // Act / Assert
        Assert.Throws<DomainException>(() => suggestion.Reject(" ", Anna, null, At));
        suggestion.Reject("Only a refactoring", Anna, null, At);
        Assert.Equal("Only a refactoring", suggestion.RejectionReason);
    }
}
