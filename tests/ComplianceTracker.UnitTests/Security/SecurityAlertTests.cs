using System.Text;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;

namespace ComplianceTracker.UnitTests.Security;

// Edge-case unit tests of US-160: GitHub alerts and monitoring alerts as suggested security events. Arrange / Act / Assert.
public class SecurityAlertTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static Finding Found(ScanSource source, int number, string ruleId, FindingSeverity severity, string message, string tool)
        => Finding.Create(ProjectId, tool, GitHubAlertsSarif.Fingerprint(source, number), ruleId, severity, message, null, Guid.NewGuid());

    private static IReadOnlyList<SecurityEventSuggestion> Match(params Finding[] findings)
        => SuggestionMatcher.Match(ProjectId, findings, findings.Select(f => f.Id).ToHashSet(), new Dictionary<string, string>(), [], [], Now);

    private static IReadOnlyList<MonitoringAlert>? Parse(string json) => MonitoringAlertParser.Parse(Encoding.UTF8.GetBytes(json));

    // ---------------------------------------------------------- GitHub (R1, R2)

    [Theory]
    [InlineData(ScanSource.GitHubDependabot, "Dependabot")]
    [InlineData(ScanSource.GitHubCodeScanning, "Code scanning")]
    [InlineData(ScanSource.GitHubSecretScanning, "Secret scanning")]
    public void The_source_is_read_back_from_the_fingerprint(ScanSource source, string name)
    {
        // Act / Assert
        Assert.Equal(name, GitHubAlertsSarif.SourceOf(GitHubAlertsSarif.Fingerprint(source, 12)));
    }

    [Theory]
    [InlineData("js/xss-abc")]
    [InlineData("")]
    [InlineData("github:unknown-1")]
    public void Another_scanner_has_no_GitHub_source(string fingerprint)
    {
        Assert.Null(GitHubAlertsSarif.SourceOf(fingerprint));
    }

    [Fact]
    public void A_high_GitHub_alert_is_serious_but_a_high_finding_from_another_scanner_is_not()
    {
        // Arrange
        var github = Found(ScanSource.GitHubCodeScanning, 1, "js/xss", FindingSeverity.High, "XSS", "CodeQL");
        var trivy = Finding.Create(ProjectId, "Trivy", "trivy-1", "js/xss", FindingSeverity.High, "XSS", null, Guid.NewGuid());
        var medium = Found(ScanSource.GitHubCodeScanning, 2, "js/xss", FindingSeverity.Medium, "XSS", "CodeQL");

        // Act / Assert
        Assert.Equal((true, false, false), (SuggestionMatcher.Serious(github), SuggestionMatcher.Serious(trivy), SuggestionMatcher.Serious(medium)));
    }

    [Theory]
    [InlineData("Prototype pollution (npm: left-pad)", "left-pad")]
    [InlineData("Prototype pollution (left-pad)", "left-pad")]
    [InlineData("Prototype pollution (npm: )", null)]
    [InlineData("Prototype pollution", null)]
    [InlineData("(maven: org.apache:log4j)  ", "org.apache:log4j")]
    [InlineData(null, null)]
    public void The_package_is_read_from_a_Dependabot_message(string? message, string? package)
    {
        Assert.Equal(package, SuggestionMatcher.PackageOf(message));
    }

    [Fact]
    public void A_high_Dependabot_alert_names_its_package_and_GitHub()
    {
        // Arrange
        var alert = Found(ScanSource.GitHubDependabot, 3, "CVE-2026-1234", FindingSeverity.High, "Prototype pollution (npm: left-pad)", "Dependabot");

        // Act
        var suggestion = Assert.Single(Match(alert));

        // Assert
        Assert.Equal(("CVE-2026-1234", "Vulnerability in left-pad (CVE-2026-1234)", SuggestionSource.GitHubAlert, "Dependabot", SecurityEventKind.VulnerabilityNotExploited),
            (suggestion.Key, suggestion.Title, suggestion.Source, suggestion.SourceName, suggestion.Kind));
    }

    [Fact]
    public void A_leaked_secret_is_an_incident_named_by_its_type()
    {
        // Arrange
        var secret = Found(ScanSource.GitHubSecretScanning, 5, "github_personal_access_token", FindingSeverity.Critical, "GitHub Personal Access Token", "Secret scanning");

        // Act
        var suggestion = Assert.Single(Match(secret));

        // Assert
        Assert.Equal(("Leaked secret: GitHub Personal Access Token", SecurityEventKind.IncidentNotSevere, "Secret scanning"),
            (suggestion.Title, suggestion.Kind, suggestion.SourceName));
    }

    [Fact]
    public void A_medium_Dependabot_alert_is_not_suggested()
    {
        Assert.Empty(Match(Found(ScanSource.GitHubDependabot, 3, "CVE-2026-1234", FindingSeverity.Medium, "Prototype pollution (npm: left-pad)", "Dependabot")));
    }

    [Fact]
    public void Only_GitHub_and_monitoring_may_suggest_an_incident()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => SecurityEventSuggestion.Create(ProjectId, "k", "t", SuggestionSource.CriticalFinding, "Trivy", SecurityEventKind.IncidentNotSevere, null, [], Now));
        Assert.Equal(SuggestionSource.Monitoring,
            SecurityEventSuggestion.Create(ProjectId, "k", "t", SuggestionSource.Monitoring, "Monitoring", SecurityEventKind.IncidentNotSevere, null, [], Now).Source);
    }

    // ---------------------------------------------------------- Parsing (R4, R6)

    [Fact]
    public void The_simple_shape_is_read()
    {
        var alert = Assert.Single(Parse("""{"alerts":[{"id":"abc","name":"HighErrorRate","status":"firing","severity":"CRITICAL","summary":" Errors "}]}""")!);

        Assert.Equal(new MonitoringAlert("abc", "HighErrorRate", true, "critical", "Errors"), alert);
    }

    [Fact]
    public void Alertmanager_labels_win_and_the_fingerprint_is_the_id()
    {
        var alert = Assert.Single(Parse("""{"version":"4","alerts":[{"status":"resolved","labels":{"alertname":"DiskFull","severity":"high"},"annotations":{"summary":"98%"},"fingerprint":"f00d","name":"ignored"}]}""")!);

        Assert.Equal(new MonitoringAlert("f00d", "DiskFull", false, "high", "98%"), alert);
    }

    [Fact]
    public void Without_an_id_the_name_is_the_id_and_an_empty_summary_is_none()
    {
        var alert = Assert.Single(Parse("""{"alerts":[{"name":"HighErrorRate","status":"firing","summary":"  "}]}""")!);

        Assert.Equal(("HighErrorRate", "", (string?)null), (alert.Id, alert.Severity, alert.Summary));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"alerts":{}}""")]
    [InlineData("""{"alerts":[1]}""")]
    [InlineData("""{"alerts":[{"name":"X","status":"pending"}]}""")]
    [InlineData("""{"alerts":[{"name":" ","status":"firing"}]}""")]
    [InlineData("""{"alerts":[{"name":5,"status":"firing"}]}""")]
    public void An_unknown_shape_is_refused(string json)
    {
        Assert.Null(Parse(json));
    }

    [Fact]
    public void An_empty_list_is_a_known_shape()
    {
        Assert.Empty(Parse("""{"alerts":[]}""")!);
    }

    [Fact]
    public void Deep_nesting_is_refused()
    {
        var deep = """{"alerts":[{"name":"X","status":"firing","x":""" + new string('[', 40) + new string(']', 40) + "}]}";

        Assert.Null(Parse(deep));
    }

    [Fact]
    public void Long_texts_are_cut()
    {
        var json = "{\"alerts\":[{\"id\":\"" + new string('i', 200) + "\",\"name\":\"" + new string('n', 200)
                   + "\",\"status\":\"firing\",\"summary\":\"" + new string('s', 400) + "\"}]}";

        var alert = Assert.Single(Parse(json)!);

        Assert.Equal((100, 150, 300), (alert.Id.Length, alert.Name.Length, alert.Summary!.Length));
    }

    [Theory]
    [InlineData("critical", true)]
    [InlineData("high", true)]
    [InlineData("warning", false)]
    [InlineData("", false)]
    public void Only_critical_and_high_are_serious(string severity, bool serious)
    {
        Assert.Equal(serious, MonitoringAlertParser.Serious(new MonitoringAlert("a", "A", true, severity, null)));
    }

    // ---------------------------------------------------------- Applying (R4)

    private static MonitoringAlert Firing(string id = "abc", string severity = "critical", string? summary = "Errors above 5%")
        => new(id, "HighErrorRate", true, severity, summary);

    private static MonitoringAlert Resolved(string id = "abc") => new(id, "HighErrorRate", false, "critical", null);

    [Fact]
    public void A_firing_serious_alert_is_suggested_once()
    {
        // Act
        var (added, closed) = MonitoringAlertService.Apply(ProjectId, [Firing(), Firing()], [], Now);

        // Assert
        var suggestion = Assert.Single(added);
        Assert.Equal(("monitoring:abc", "HighErrorRate: Errors above 5%", SecurityEventKind.IncidentNotSevere, 0),
            (suggestion.Key, suggestion.Title, suggestion.Kind, closed));
    }

    [Fact]
    public void An_alert_that_still_fires_is_not_suggested_again()
    {
        var (first, _) = MonitoringAlertService.Apply(ProjectId, [Firing()], [], Now);

        var (added, _) = MonitoringAlertService.Apply(ProjectId, [Firing()], first, Now.AddMinutes(5));

        Assert.Empty(added);
    }

    [Fact]
    public void Resolved_closes_a_pending_suggestion_with_the_reason()
    {
        var (first, _) = MonitoringAlertService.Apply(ProjectId, [Firing(summary: null)], [], Now);

        var (added, closed) = MonitoringAlertService.Apply(ProjectId, [Resolved()], first, Now.AddMinutes(5));

        Assert.Equal((0, 1, false, "Resolved at the source", "HighErrorRate"),
            (added.Count, closed, first[0].IsPending, first[0].DismissedReason, first[0].Title));
    }

    [Fact]
    public void Firing_and_resolved_in_the_same_message_leave_nothing()
    {
        var (added, closed) = MonitoringAlertService.Apply(ProjectId, [Firing(), Resolved()], [], Now);

        Assert.Equal((0, 0), (added.Count, closed));
    }

    [Fact]
    public void A_decided_suggestion_is_not_closed_but_the_alert_may_be_suggested_again()
    {
        // Arrange: a person dismissed it
        var (first, _) = MonitoringAlertService.Apply(ProjectId, [Firing()], [], Now);
        first[0].Supersede("Handled", Now);

        // Act
        var (_, closed) = MonitoringAlertService.Apply(ProjectId, [Resolved()], first, Now.AddMinutes(1));
        var (again, _) = MonitoringAlertService.Apply(ProjectId, [Firing()], first, Now.AddMinutes(2));

        // Assert
        Assert.Equal((0, "Handled", 1), (closed, first[0].DismissedReason, again.Count));
    }

    [Fact]
    public void A_warning_or_an_unknown_resolved_alert_does_nothing()
    {
        var (added, closed) = MonitoringAlertService.Apply(ProjectId, [Firing(severity: "warning"), Resolved("other")], [], Now);

        Assert.Equal((0, 0), (added.Count, closed));
    }
}
