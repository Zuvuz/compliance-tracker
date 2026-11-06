using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Scanning;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ComplianceTracker.Infrastructure.WorkItems
{
    // US-121: the repository's code scanning and Dependabot alerts, read-only (B12), with the project's token.
    // GET /repos/{owner}/{repo}/code-scanning/alerts and /dependabot/alerts, open and dismissed, every page.
    public partial class GitHubWorkItemSource : IGitHubSecurityAlerts
    {
        public async Task<IReadOnlyList<GitHubAlertRead>> ReadAsync(string repository, string? token, CancellationToken cancellationToken = default)
        {
            var codeScanning = await ReadSourceAsync<CodeScanningAlert>(repository, "code-scanning", token, cancellationToken);
            var dependabot = await ReadSourceAsync<DependabotAlert>(repository, "dependabot", token, cancellationToken);

            return
            [
                codeScanning.Unavailable is { } c
                    ? new GitHubAlertRead(ScanSource.GitHubCodeScanning, [], c)
                    : new GitHubAlertRead(ScanSource.GitHubCodeScanning, codeScanning.Alerts.Select(ToAlert).ToList()),
                dependabot.Unavailable is { } d
                    ? new GitHubAlertRead(ScanSource.GitHubDependabot, [], d)
                    : new GitHubAlertRead(ScanSource.GitHubDependabot, dependabot.Alerts.Select(ToAlert).ToList())
            ];
        }

        // R5: a source that is switched off, not set up or not allowed is said in words; the other is still read
        private async Task<(List<T> Alerts, string? Unavailable)> ReadSourceAsync<T>(
            string repository, string kind, string? token, CancellationToken cancellationToken)
        {
            var name = kind == "dependabot" ? "Dependabot alerts" : "Code scanning alerts";
            var alerts = new List<T>();
            foreach (var state in new[] { "open", "dismissed" })
            {
                for (var page = 1; page <= 50; page++)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get,
                        $"repos/{repository}/{kind}/alerts?state={state}&per_page={PageSize}&page={page}");
                    if (token is not null)
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }

                    HttpResponseMessage response;
                    try
                    {
                        response = await _http.SendAsync(request, cancellationToken);
                    }
                    catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                    {
                        throw new WorkItemSourceException("GitHub could not be reached. Try again later", error);
                    }

                    using (response)
                    {
                        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
                        {
                            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                            return ([], Unavailable(name, response.StatusCode, detail));
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            return ([], $"{name}: GitHub answered with an error ({(int)response.StatusCode})");
                        }

                        var list = await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken) ?? [];
                        alerts.AddRange(list);
                        if (list.Count < PageSize)
                        {
                            break;
                        }
                    }
                }
            }

            return (alerts, null);
        }

        // e.g. "Dependabot alerts are switched off for this repository. Switch them on under Settings → Code security."
        private static string Unavailable(string name, HttpStatusCode status, string detail)
        {
            if (detail.Contains("disabled", StringComparison.OrdinalIgnoreCase))
            {
                return $"{name} are switched off for this repository. Switch them on under Settings → Code security.";
            }

            if (detail.Contains("no analysis found", StringComparison.OrdinalIgnoreCase) || detail.Contains("not enabled", StringComparison.OrdinalIgnoreCase))
            {
                return $"{name}: code scanning is not set up for this repository.";
            }

            return status == HttpStatusCode.NotFound
                ? $"{name}: the repository was not found, or the token cannot read its security alerts."
                : $"{name}: the token cannot read the repository's security alerts (it needs read access to security events).";
        }

        private static GitHubAlert ToAlert(CodeScanningAlert alert)
        {
            var instance = alert.MostRecentInstance;
            var line = instance?.Location?.StartLine is { } start ? $":{start}" : "";
            return new GitHubAlert(
                alert.Number,
                alert.Tool?.Name ?? "Code scanning",
                alert.Rule?.Id ?? "",
                Severity(alert.Rule?.SecuritySeverityLevel ?? alert.Rule?.Severity),
                instance?.Message?.Text ?? alert.Rule?.Description ?? "",
                instance?.Location?.Path is { } path ? path + line : null,
                alert.State == "dismissed",
                alert.DismissedReason,
                alert.DismissedComment);
        }

        private static GitHubAlert ToAlert(DependabotAlert alert)
        {
            var advisory = alert.SecurityAdvisory;
            var package = alert.SecurityVulnerability?.Package;
            return new GitHubAlert(
                alert.Number,
                "Dependabot",
                advisory?.CveId ?? advisory?.GhsaId ?? "",
                Severity(alert.SecurityVulnerability?.Severity ?? advisory?.Severity),
                $"{advisory?.Summary}{(package?.Name is { } name ? $" ({package.Ecosystem}: {name})" : "")}".Trim(),
                alert.Dependency?.ManifestPath,
                alert.State is "dismissed" or "auto_dismissed",
                alert.DismissedReason ?? (alert.State == "auto_dismissed" ? "auto-dismissed" : null),
                alert.DismissedComment);
        }

        // GitHub's words: critical, high, medium, low; or a rule's level: error, warning, note
        private static FindingSeverity Severity(string? level) => level?.ToLowerInvariant() switch
        {
            "critical" => FindingSeverity.Critical,
            "high" or "error" => FindingSeverity.High,
            "medium" or "moderate" or "warning" => FindingSeverity.Medium,
            _ => FindingSeverity.Low
        };

        private sealed class CodeScanningAlert
        {
            [JsonPropertyName("number")] public int Number { get; set; }
            [JsonPropertyName("state")] public string? State { get; set; }
            [JsonPropertyName("dismissed_reason")] public string? DismissedReason { get; set; }
            [JsonPropertyName("dismissed_comment")] public string? DismissedComment { get; set; }
            [JsonPropertyName("rule")] public CodeScanningRule? Rule { get; set; }
            [JsonPropertyName("tool")] public CodeScanningTool? Tool { get; set; }
            [JsonPropertyName("most_recent_instance")] public CodeScanningInstance? MostRecentInstance { get; set; }
        }

        private sealed class CodeScanningRule
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("severity")] public string? Severity { get; set; }
            [JsonPropertyName("security_severity_level")] public string? SecuritySeverityLevel { get; set; }
            [JsonPropertyName("description")] public string? Description { get; set; }
        }

        private sealed class CodeScanningTool
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
        }

        private sealed class CodeScanningInstance
        {
            [JsonPropertyName("message")] public CodeScanningMessage? Message { get; set; }
            [JsonPropertyName("location")] public CodeScanningLocation? Location { get; set; }
        }

        private sealed class CodeScanningMessage
        {
            [JsonPropertyName("text")] public string? Text { get; set; }
        }

        private sealed class CodeScanningLocation
        {
            [JsonPropertyName("path")] public string? Path { get; set; }
            [JsonPropertyName("start_line")] public int? StartLine { get; set; }
        }

        private sealed class DependabotAlert
        {
            [JsonPropertyName("number")] public int Number { get; set; }
            [JsonPropertyName("state")] public string? State { get; set; }
            [JsonPropertyName("dismissed_reason")] public string? DismissedReason { get; set; }
            [JsonPropertyName("dismissed_comment")] public string? DismissedComment { get; set; }
            [JsonPropertyName("security_advisory")] public DependabotAdvisory? SecurityAdvisory { get; set; }
            [JsonPropertyName("security_vulnerability")] public DependabotVulnerability? SecurityVulnerability { get; set; }
            [JsonPropertyName("dependency")] public DependabotDependency? Dependency { get; set; }
        }

        private sealed class DependabotAdvisory
        {
            [JsonPropertyName("ghsa_id")] public string? GhsaId { get; set; }
            [JsonPropertyName("cve_id")] public string? CveId { get; set; }
            [JsonPropertyName("summary")] public string? Summary { get; set; }
            [JsonPropertyName("severity")] public string? Severity { get; set; }
        }

        private sealed class DependabotVulnerability
        {
            [JsonPropertyName("severity")] public string? Severity { get; set; }
            [JsonPropertyName("package")] public DependabotPackage? Package { get; set; }
        }

        private sealed class DependabotPackage
        {
            [JsonPropertyName("ecosystem")] public string? Ecosystem { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
        }

        private sealed class DependabotDependency
        {
            [JsonPropertyName("manifest_path")] public string? ManifestPath { get; set; }
        }
    }
}
