using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Infrastructure.Scanning;
using ComplianceTracker.Infrastructure.WorkItems;
using System.Net;
using System.Text;

namespace ComplianceTracker.UnitTests.Scanning;

// Edge cases of fetching GitHub's security alerts (US-121): GitHub's answers, the conversion to SARIF and back.
// Arrange / Act / Assert.
public class GitHubAlertsTests
{
    // Answers each request with what the test gives for its path; records the paths
    private sealed class Answers : HttpMessageHandler
    {
        private readonly Func<string, (HttpStatusCode Status, string Body)> _answer;
        public List<string> Paths { get; } = new();

        public Answers(Func<string, (HttpStatusCode, string)> answer) => _answer = answer;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Paths.Add(path);
            var (status, body) = _answer(path);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static GitHubWorkItemSource Source(Answers answers)
        => new(new HttpClient(answers) { BaseAddress = new Uri("https://api.github.invalid/") });

    private const string OneCodeQlAlert = """
        [{ "number": 7, "state": "open", "rule": { "id": "js/xss", "severity": "error", "security_severity_level": "medium" },
           "tool": { "name": "CodeQL" }, "most_recent_instance": { "message": { "text": "XSS" }, "location": { "path": "a.js", "start_line": 4 } } }]
        """;

    // ---------------------------------------------------------- GitHub's answers

    [Fact]
    public async Task Dependabot_switched_off_is_said_in_words_and_code_scanning_is_still_read()
    {
        // Arrange
        var answers = new Answers(path => path.Contains("dependabot")
            ? (HttpStatusCode.Forbidden, "{\"message\":\"Dependabot alerts are disabled for this repository.\"}")
            : (HttpStatusCode.OK, path.Contains("state=open") ? OneCodeQlAlert : "[]"));

        // Act
        var reads = await Source(answers).ReadAsync("acme/portal", null);

        // Assert
        Assert.Single(reads.Single(r => r.Source == ScanSource.GitHubCodeScanning).Alerts);
        Assert.StartsWith("Dependabot alerts are switched off", reads.Single(r => r.Source == ScanSource.GitHubDependabot).Unavailable);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{\"message\":\"no analysis found\"}", "code scanning is not set up")]
    [InlineData(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}", "the repository was not found")]
    [InlineData(HttpStatusCode.Forbidden, "{\"message\":\"Resource not accessible\"}", "the token cannot read")]
    [InlineData(HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}", "the token cannot read")]
    [InlineData(HttpStatusCode.InternalServerError, "", "GitHub answered with an error (500)")]
    public async Task A_source_that_cannot_be_read_says_why(HttpStatusCode status, string body, string reason)
    {
        // Arrange
        var answers = new Answers(path => path.Contains("code-scanning") ? (status, body) : (HttpStatusCode.OK, "[]"));

        // Act
        var reads = await Source(answers).ReadAsync("acme/portal", "token");

        // Assert
        Assert.Contains(reason, reads.Single(r => r.Source == ScanSource.GitHubCodeScanning).Unavailable);
    }

    [Fact]
    public async Task Open_and_dismissed_alerts_are_read_but_not_fixed_ones()
    {
        // Arrange
        var answers = new Answers(_ => (HttpStatusCode.OK, "[]"));

        // Act
        await Source(answers).ReadAsync("acme/portal", null);

        // Assert
        Assert.Contains(answers.Paths, p => p.Contains("code-scanning/alerts?state=open"));
        Assert.Contains(answers.Paths, p => p.Contains("code-scanning/alerts?state=dismissed"));
        Assert.DoesNotContain(answers.Paths, p => p.Contains("state=fixed"));
    }

    [Fact]
    public async Task A_full_page_asks_for_the_next_one_and_a_short_page_stops()
    {
        // Arrange: 100 alerts on page 1, 1 on page 2
        var full = "[" + string.Join(",", Enumerable.Range(1, 100).Select(i => $"{{\"number\":{i},\"state\":\"open\"}}")) + "]";
        var answers = new Answers(path => path.Contains("code-scanning") && path.Contains("state=open")
            ? (HttpStatusCode.OK, path.EndsWith("&page=1", StringComparison.Ordinal) ? full : "[{\"number\":101,\"state\":\"open\"}]")
            : (HttpStatusCode.OK, "[]"));

        // Act
        var reads = await Source(answers).ReadAsync("acme/portal", null);

        // Assert
        Assert.Equal(101, reads.Single(r => r.Source == ScanSource.GitHubCodeScanning).Alerts.Count);
        Assert.DoesNotContain(answers.Paths, p => p.Contains("code-scanning") && p.Contains("state=open") && p.EndsWith("&page=3", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("critical", FindingSeverity.Critical)]
    [InlineData("high", FindingSeverity.High)]
    [InlineData("moderate", FindingSeverity.Medium)]
    [InlineData("medium", FindingSeverity.Medium)]
    [InlineData("low", FindingSeverity.Low)]
    [InlineData("unknown", FindingSeverity.Low)]
    public async Task Dependabot_severities_are_read(string severity, FindingSeverity expected)
    {
        // Arrange
        var alert = $$"""[{ "number": 3, "state": "open", "security_advisory": { "cve_id": "CVE-1", "summary": "s" }, "security_vulnerability": { "severity": "{{severity}}" } }]""";
        var answers = new Answers(path => (HttpStatusCode.OK, path.Contains("dependabot") && path.Contains("state=open") ? alert : "[]"));

        // Act
        var reads = await Source(answers).ReadAsync("acme/portal", null);

        // Assert
        Assert.Equal(expected, reads.Single(r => r.Source == ScanSource.GitHubDependabot).Alerts.Single().Severity);
    }

    [Fact]
    public async Task A_Dependabot_alert_without_a_CVE_uses_its_GHSA_id_and_auto_dismissed_counts_as_dismissed()
    {
        // Arrange
        var alert = """[{ "number": 3, "state": "auto_dismissed", "security_advisory": { "ghsa_id": "GHSA-xxxx", "summary": "s", "severity": "low" } }]""";
        var answers = new Answers(path => (HttpStatusCode.OK, path.Contains("dependabot") && path.Contains("state=dismissed") ? alert : "[]"));

        // Act
        var read = (await Source(answers).ReadAsync("acme/portal", null)).Single(r => r.Source == ScanSource.GitHubDependabot).Alerts.Single();

        // Assert
        Assert.Equal("GHSA-xxxx", read.RuleId);
        Assert.True(read.Dismissed);
        Assert.Equal("auto-dismissed", read.DismissedReason);
    }

    // ---------------------------------------------------------- The conversion to SARIF

    [Fact]
    public void The_converted_alerts_are_read_back_with_the_same_fingerprints_and_severities()
    {
        // Arrange
        GitHubAlert[] alerts =
        [
            new(7, "CodeQL", "js/xss", FindingSeverity.Critical, "XSS", "a.js:4"),
            new(8, "CodeQL", "cs/sql", FindingSeverity.Low, "SQL", null)
        ];

        // Act
        var report = new SarifScanImporter().Read(GitHubAlertsSarif.Build(ScanSource.GitHubCodeScanning, "CodeQL", alerts));

        // Assert
        Assert.Equal("CodeQL", report.Tool);
        Assert.Equal(GitHubAlertsSarif.Fingerprint(ScanSource.GitHubCodeScanning, 7), report.Results[0].Fingerprint);
        Assert.Equal([FindingSeverity.Critical, FindingSeverity.Low], report.Results.Select(r => r.Severity));
        Assert.Equal("a.js:4", report.Results[0].Location);
        Assert.Null(report.Results[1].Location);
    }

    [Fact]
    public void No_open_alerts_give_a_scan_without_results_so_the_old_ones_are_fixed()
    {
        // Arrange (nothing)

        // Act
        var content = GitHubAlertsSarif.Build(ScanSource.GitHubDependabot, "Dependabot", []);

        // Assert
        Assert.True(new SarifScanImporter().CanRead("x.sarif", content));
        Assert.Empty(new SarifScanImporter().Read(content).Results);
    }

    [Fact]
    public void The_same_number_in_the_two_sources_gives_two_fingerprints()
    {
        // Arrange (nothing)

        // Act
        var codeScanning = GitHubAlertsSarif.Fingerprint(ScanSource.GitHubCodeScanning, 3);
        var dependabot = GitHubAlertsSarif.Fingerprint(ScanSource.GitHubDependabot, 3);

        // Assert
        Assert.NotEqual(codeScanning, dependabot);
    }

    [Theory]
    [InlineData("false positive", null, "Dismissed in GitHub: false positive")]
    [InlineData("won't fix", "not reachable", "Dismissed in GitHub: won't fix (not reachable)")]
    [InlineData(null, null, "Dismissed in GitHub: no reason given")]
    [InlineData(" ", " ", "Dismissed in GitHub: no reason given")]
    public void The_dismiss_reason_says_what_GitHub_said(string? reason, string? comment, string expected)
    {
        // Arrange
        var alert = new GitHubAlert(1, "CodeQL", "r", FindingSeverity.Low, "m", null, true, reason, comment);

        // Act
        var text = GitHubAlertsSarif.DismissReason(alert);

        // Assert
        Assert.Equal(expected, text);
    }
}
