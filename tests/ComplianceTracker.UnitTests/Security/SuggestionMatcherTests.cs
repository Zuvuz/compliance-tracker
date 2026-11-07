using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Security;

// Edge-case unit tests of US-128: which findings give which suggestions. Arrange / Act / Assert.
public class SuggestionMatcherTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 8, 12, 0, DateTimeKind.Utc);
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly IReadOnlySet<Guid> NoneNew = new HashSet<Guid>();

    private static Finding Found(string ruleId, FindingSeverity severity = FindingSeverity.High, string message = "A vulnerable package", string tool = "Trivy")
        => Finding.Create(ProjectId, tool, ruleId.ToLowerInvariant() + Guid.NewGuid(), ruleId, severity, message, "package-lock.json", Guid.NewGuid());

    private static Dictionary<string, string> Kev(params string[] cves) => cves.ToDictionary(c => c, _ => "CISA KEV");

    private static IReadOnlyList<SecurityEventSuggestion> Match(
        IReadOnlyList<Finding> findings, IReadOnlyDictionary<string, string>? exploited = null, IReadOnlySet<Guid>? newIds = null,
        IReadOnlyList<SecurityEvent>? events = null, IReadOnlyList<SecurityEventSuggestion>? existing = null)
        => SuggestionMatcher.Match(ProjectId, findings, newIds ?? NoneNew, exploited ?? Kev(), events ?? [], existing ?? [], Now);

    [Theory]
    [InlineData("CVE-2026-1234", "A vulnerable package")] // Trivy: the rule id
    [InlineData("js/sql-injection", "Fixed in 2.1 (cve-2026-1234)")] // lower case, in the message
    public void An_exploited_CVE_is_found_in_the_rule_id_or_the_message(string ruleId, string message)
    {
        // Arrange
        var finding = Found(ruleId, message: message);

        // Act
        var suggestion = Assert.Single(Match([finding], Kev("CVE-2026-1234")));

        // Assert: R2
        Assert.Equal("CVE-2026-1234", suggestion.Key);
        Assert.Equal(SecurityEventKind.ActivelyExploitedVulnerability, suggestion.Kind);
        Assert.Equal("CISA KEV", suggestion.SourceName);
        Assert.Equal(Now, suggestion.LearnedAt);
    }

    [Fact]
    public void The_same_CVE_in_several_findings_gives_one_suggestion()
    {
        // Arrange
        var first = Found("CVE-2026-1234");
        var second = Found("CVE-2026-1234", tool: "Grype");

        // Act
        var suggestion = Assert.Single(Match([first, second], Kev("CVE-2026-1234")));

        // Assert
        Assert.Equal(new[] { first.Id, second.Id }.Order(), suggestion.FindingIds.Order());
    }

    [Fact]
    public void A_finding_without_a_CVE_is_not_matched_against_the_lists()
    {
        // Arrange / Act / Assert
        Assert.Empty(Match([Found("js/xss", message: "No CVE here, only CVE-12")], Kev("CVE-2026-1234")));
    }

    [Fact]
    public void An_empty_list_suggests_nothing_for_old_findings()
    {
        // Arrange / Act / Assert
        Assert.Empty(Match([Found("CVE-2026-1234", FindingSeverity.Critical)]));
    }

    [Fact]
    public void A_fixed_or_accepted_finding_is_not_matched()
    {
        // Arrange
        var accepted = Found("CVE-2026-1234");
        accepted.Accept("Not used", Anna, Now);

        // Act / Assert
        Assert.Empty(Match([accepted], Kev("CVE-2026-1234")));
    }

    [Fact]
    public void Only_a_new_critical_finding_suggests_a_vulnerability_that_is_not_exploited()
    {
        // Arrange
        var old = Found("CVE-2026-1111", FindingSeverity.Critical);
        var high = Found("CVE-2026-2222", FindingSeverity.High);
        var fresh = Found("CVE-2026-3333", FindingSeverity.Critical);

        // Act
        var suggestion = Assert.Single(Match([old, high, fresh], newIds: new HashSet<Guid> { high.Id, fresh.Id }));

        // Assert: R3
        Assert.Equal("CVE-2026-3333", suggestion.Key);
        Assert.Equal(SecurityEventKind.VulnerabilityNotExploited, suggestion.Kind);
        Assert.Equal("Trivy", suggestion.SourceName);
    }

    [Fact]
    public void A_new_critical_finding_without_a_CVE_is_suggested_by_its_rule()
    {
        // Arrange
        var finding = Found("js/sql-injection", FindingSeverity.Critical, "SQL built from user input");

        // Act
        var suggestion = Assert.Single(Match([finding], newIds: new HashSet<Guid> { finding.Id }));

        // Assert
        Assert.Equal($"finding:{finding.Id}", suggestion.Key);
        Assert.Equal("js/sql-injection: SQL built from user input", suggestion.Title);
    }

    [Fact]
    public void A_very_long_finding_gives_a_title_that_fits()
    {
        // Arrange
        var finding = Found("js/sql-injection", FindingSeverity.Critical, new string('x', 1000));

        // Act
        var suggestion = Assert.Single(Match([finding], newIds: new HashSet<Guid> { finding.Id }));

        // Assert
        Assert.Equal(SecurityEvent.MaxTitleLength, suggestion.Title.Length);
        Assert.EndsWith("…", suggestion.Title);
    }

    [Fact]
    public void A_CVE_with_a_reportable_or_closed_event_is_not_suggested_again()
    {
        // Arrange
        var reportable = SecurityEvent.Record(ProjectId, SecurityEventKind.ActivelyExploitedVulnerability, "CVE-2026-1234", null, Now, Anna, UserRole.Developer, Now);
        var closed = SecurityEvent.Record(ProjectId, SecurityEventKind.VulnerabilityNotExploited, "Old", "Was CVE-2026-5678", Now, Anna, UserRole.Developer, Now);
        closed.Close(Anna, Now, "Removed");

        // Act / Assert: R4
        Assert.Empty(Match([Found("CVE-2026-1234"), Found("CVE-2026-5678")], Kev("CVE-2026-1234", "CVE-2026-5678"), events: [reportable, closed]));
    }

    [Fact]
    public void An_open_event_that_is_not_exploited_gets_an_upgrade_suggestion()
    {
        // Arrange
        var notExploited = SecurityEvent.Record(ProjectId, SecurityEventKind.VulnerabilityNotExploited, "CVE-2026-1234 in the parser", null, Now.AddDays(-2), Anna, UserRole.Developer, Now);

        // Act
        var suggestion = Assert.Single(Match([Found("CVE-2026-1234")], Kev("CVE-2026-1234"), events: [notExploited]));

        // Assert
        Assert.Equal(notExploited.Id, suggestion.UpgradeEventId);
        Assert.Equal("CVE-2026-1234 in the parser", suggestion.Title);
    }

    [Fact]
    public void A_pending_suggestion_is_not_repeated()
    {
        // Arrange
        var finding = Found("CVE-2026-1234");
        var first = Match([finding], Kev("CVE-2026-1234"));

        // Act / Assert
        Assert.Empty(Match([finding], Kev("CVE-2026-1234"), existing: first));
    }

    [Fact]
    public void A_dismissed_suggestion_comes_back_only_for_a_new_finding()
    {
        // Arrange
        var finding = Found("CVE-2026-1234");
        var dismissed = Assert.Single(Match([finding], Kev("CVE-2026-1234")));
        dismissed.Dismiss("Not used", Anna, Now);

        // Act / Assert: R6
        Assert.Empty(Match([finding], Kev("CVE-2026-1234"), existing: [dismissed]));
        var again = Assert.Single(Match([finding, Found("CVE-2026-1234", tool: "Grype")], Kev("CVE-2026-1234"), existing: [dismissed]));
        Assert.True(again.IsPending);
    }

    [Fact]
    public void A_pending_not_exploited_suggestion_is_superseded_when_the_CVE_becomes_exploited()
    {
        // Arrange
        var finding = Found("CVE-2026-1234", FindingSeverity.Critical);
        var notExploited = Assert.Single(Match([finding], newIds: new HashSet<Guid> { finding.Id }));

        // Act
        var exploited = Assert.Single(Match([finding], Kev("CVE-2026-1234"), existing: [notExploited]));

        // Assert
        Assert.Equal(SecurityEventKind.ActivelyExploitedVulnerability, exploited.Kind);
        Assert.Equal(SuggestionStatus.Dismissed, notExploited.Status);
        Assert.Equal("Now actively exploited", notExploited.DismissedReason);
    }

    [Fact]
    public void An_upgrade_suggestion_is_withdrawn_when_the_event_was_upgraded_another_way()
    {
        // Arrange
        var securityEvent = SecurityEvent.Record(ProjectId, SecurityEventKind.VulnerabilityNotExploited, "CVE-2026-1234", null, Now.AddDays(-2), Anna, UserRole.Developer, Now);
        var finding = Found("CVE-2026-1234");
        var upgrade = Assert.Single(Match([finding], Kev("CVE-2026-1234"), events: [securityEvent]));
        securityEvent.Upgrade(Now.AddHours(-1), "By hand", Anna, Now);

        // Act
        var added = Match([finding], Kev("CVE-2026-1234"), events: [securityEvent], existing: [upgrade]);

        // Assert
        Assert.Empty(added);
        Assert.False(upgrade.IsPending);
    }

    [Theory]
    [InlineData("CVE-2026-1234 and cve-2026-1234", 1)]
    [InlineData("CVE-2026-123", 0)] // too short
    [InlineData("XCVE-2026-12345", 0)] // not on its own
    [InlineData("CVE-2025-0001, CVE-2026-1234567", 2)]
    [InlineData("", 0)]
    public void CVE_ids_are_found_once_and_in_upper_case(string text, int count)
    {
        // Arrange / Act
        var ids = CveIds.In(text, null);

        // Assert
        Assert.Equal(count, ids.Count);
        Assert.All(ids, id => Assert.Equal(id.ToUpperInvariant(), id));
    }
}
