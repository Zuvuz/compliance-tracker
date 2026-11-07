using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Security;

namespace ComplianceTracker.Domain.Tests.Security;

// Edge-case unit tests of US-128's SecurityEventSuggestion and ExploitedList. Arrange / Act / Assert.
public class SecurityEventSuggestionTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 8, 12, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static SecurityEventSuggestion Suggest(SecurityEventKind kind = SecurityEventKind.ActivelyExploitedVulnerability, Guid? upgrade = null)
        => SecurityEventSuggestion.Create(Guid.NewGuid(), " CVE-2026-1234 ", "", SuggestionSource.ExploitedList, "CISA KEV", kind, upgrade, [Guid.Empty, Guid.Empty], Now);

    [Fact]
    public void A_suggestion_without_a_title_uses_its_key()
    {
        // Arrange / Act
        var suggestion = Suggest();

        // Assert
        Assert.Equal("CVE-2026-1234", suggestion.Key);
        Assert.Equal("CVE-2026-1234", suggestion.Title);
        Assert.Single(suggestion.FindingIds);
        Assert.True(suggestion.IsPending);
    }

    [Theory]
    [InlineData(SecurityEventKind.SevereIncident)]
    [InlineData(SecurityEventKind.IncidentNotSevere)]
    public void Only_vulnerabilities_are_suggested(SecurityEventKind kind)
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => Suggest(kind));
    }

    [Fact]
    public void An_upgrade_is_always_to_actively_exploited()
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => Suggest(SecurityEventKind.VulnerabilityNotExploited, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Dismissing_needs_a_reason(string? reason)
    {
        // Arrange
        var suggestion = Suggest();

        // Act / Assert
        Assert.Throws<DomainException>(() => suggestion.Dismiss(reason, Anna, Now));
        Assert.True(suggestion.IsPending);
    }

    [Fact]
    public void A_decided_suggestion_cannot_be_decided_again()
    {
        // Arrange
        var suggestion = Suggest();
        suggestion.Accept(Anna, Now);

        // Act / Assert
        Assert.Throws<DomainException>(() => suggestion.Dismiss("Late", Anna, Now));
        Assert.Throws<DomainException>(() => suggestion.Accept(Anna, Now));
        Assert.Throws<DomainException>(() => suggestion.Supersede("Now exploited", Now));
    }

    [Fact]
    public void A_dismissal_reason_that_is_too_long_is_refused()
    {
        // Arrange
        var suggestion = Suggest();

        // Act / Assert
        Assert.Throws<DomainException>(() => suggestion.Dismiss(new string('x', SecurityEventSuggestion.MaxReasonLength + 1), Anna, Now));
    }

    [Fact]
    public void A_list_keeps_its_last_version_when_fetching_fails()
    {
        // Arrange
        var list = new ExploitedList("CISA KEV");
        list.Refreshed([" cve-2026-1234", "CVE-2026-1234", "", "CVE-2025-0001"], Now);

        // Act
        list.Failed(new string('e', 2000), Now.AddDays(1));

        // Assert: R5
        Assert.Equal(["CVE-2025-0001", "CVE-2026-1234"], list.Cves);
        Assert.Equal(Now, list.FetchedAt);
        Assert.Equal(Now.AddDays(1), list.AttemptedAt);
        Assert.Equal(ExploitedList.MaxErrorLength, list.LastError!.Length);
    }

    [Fact]
    public void A_successful_fetch_clears_the_error()
    {
        // Arrange
        var list = new ExploitedList("EUVD");
        list.Failed("Timeout", Now);

        // Act
        list.Refreshed([], Now.AddHours(1));

        // Assert
        Assert.Null(list.LastError);
        Assert.Empty(list.Cves);
    }
}
