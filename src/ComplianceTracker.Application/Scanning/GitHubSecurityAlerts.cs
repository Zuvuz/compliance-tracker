using ComplianceTracker.Domain.Scanning;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Application.Scanning
{
    // US-121: one security alert GitHub has for a repository: a code scanning alert (CodeQL and other tools that upload
    // SARIF to GitHub) or a Dependabot alert. Read-only (B12).
    public record GitHubAlert(
        int Number, string Tool, string RuleId, FindingSeverity Severity, string Message, string? Location,
        bool Dismissed = false, string? DismissedReason = null, string? DismissedComment = null);

    // R5: Unavailable says why a source could not be read (switched off, not set up, no access); the other source still is
    public record GitHubAlertRead(ScanSource Source, IReadOnlyList<GitHubAlert> Alerts, string? Unavailable = null);

    // Implemented by the GitHub adapter
    public interface IGitHubSecurityAlerts
    {
        // Open and dismissed alerts of both sources; fixed alerts are left out, so the import marks them fixed
        Task<IReadOnlyList<GitHubAlertRead>> ReadAsync(string repository, string? token, CancellationToken cancellationToken = default);
    }

    // US-121: GitHub's alerts as a SARIF 2.1.0 file, so they go through the same import as an upload (US-078): the same
    // matching by fingerprint, the same "fixed in scan of", the same log. The file is kept with the scan, so it can be
    // evidence like any other scan.
    public static class GitHubAlertsSarif
    {
        // The fingerprint of a GitHub alert, the same in every fetch: e.g. "github:code-scanning-12"
        public static string Fingerprint(ScanSource source, int number)
            => $"github:{(source == ScanSource.GitHubDependabot ? "dependabot" : "code-scanning")}-{number}";

        // A CVSS-like score for the importer's severity limits (US-078 R2)
        private static string Score(FindingSeverity severity) => severity switch
        {
            FindingSeverity.Critical => "9.5",
            FindingSeverity.High => "8.0",
            FindingSeverity.Medium => "5.0",
            _ => "2.0"
        };

        // Only the open alerts become results; the file name says the source and the tool
        public static byte[] Build(ScanSource source, string tool, IEnumerable<GitHubAlert> openAlerts)
        {
            var alerts = openAlerts.ToList();
            var key = source == ScanSource.GitHubDependabot ? "dependabot" : "code-scanning";
            var sarif = new Dictionary<string, object>
            {
                ["version"] = "2.1.0",
                ["runs"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["tool"] = new { driver = new { name = tool } },
                        ["results"] = alerts.Select(a => new Dictionary<string, object?>
                        {
                            ["ruleId"] = a.RuleId,
                            ["message"] = new { text = a.Message },
                            ["properties"] = new Dictionary<string, string> { ["security-severity"] = Score(a.Severity) },
                            ["partialFingerprints"] = new Dictionary<string, string> { ["github"] = $"{key}-{a.Number.ToString(CultureInfo.InvariantCulture)}" },
                            ["locations"] = a.Location is null
                                ? Array.Empty<object>()
                                : new object[] { new { physicalLocation = new { artifactLocation = new { uri = a.Location } } } }
                        }).ToList()
                    }
                }
            };

            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sarif));
        }

        // e.g. "Dismissed in GitHub: false positive (not reachable)"
        public static string DismissReason(GitHubAlert alert)
            => "Dismissed in GitHub: " + (string.IsNullOrWhiteSpace(alert.DismissedReason) ? "no reason given" : alert.DismissedReason.Trim())
               + (string.IsNullOrWhiteSpace(alert.DismissedComment) ? "" : $" ({alert.DismissedComment.Trim()})");
    }
}
