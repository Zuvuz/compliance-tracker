using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Infrastructure.Scanning
{
    // US-078 R1, R2: reads SARIF 2.1.0 (https://docs.oasis-open.org/sarif/sarif/v2.1.0/), the format most scanners write
    // (Trivy, Grype, Semgrep, CodeQL, ZAP …). Only the first run's tool name is used; every run's results are read.
    public class SarifScanImporter : IScanImporter
    {
        public string Format => "SARIF 2.1.0";

        // A .sarif or .json file whose JSON says version 2.1.0 and has runs
        public bool CanRead(string fileName, byte[] content)
        {
            var extension = Path.GetExtension(fileName ?? "");
            if (!extension.Equals(".sarif", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(content);
                return document.RootElement.ValueKind == JsonValueKind.Object
                       && document.RootElement.TryGetProperty("version", out var version) && version.GetString() == "2.1.0"
                       && document.RootElement.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public ScanReport Read(byte[] content)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(content);
            }
            catch (JsonException)
            {
                throw new DomainException("The SARIF file could not be read");
            }

            using (document)
            {
                var runs = document.RootElement.GetProperty("runs").EnumerateArray().ToList();
                var tool = runs.Select(ToolName).FirstOrDefault(n => n is not null) ?? "Unknown tool";
                var results = new List<ScanResult>();
                foreach (var run in runs)
                {
                    var rules = Rules(run);
                    if (!run.TryGetProperty("results", out var runResults) || runResults.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var result in runResults.EnumerateArray())
                    {
                        results.Add(Result(result, rules));
                    }
                }

                return new ScanReport(tool, results);
            }
        }

        private static string? ToolName(JsonElement run)
            => run.TryGetProperty("tool", out var tool) && tool.TryGetProperty("driver", out var driver)
               && driver.TryGetProperty("name", out var name) ? name.GetString() : null;

        // The rules' default level and "security-severity" (GitHub's convention, used by Trivy, Grype and CodeQL)
        private static Dictionary<string, (string? Level, double? SecuritySeverity)> Rules(JsonElement run)
        {
            var rules = new Dictionary<string, (string?, double?)>(StringComparer.Ordinal);
            if (run.TryGetProperty("tool", out var tool) && tool.TryGetProperty("driver", out var driver)
                && driver.TryGetProperty("rules", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in list.EnumerateArray())
                {
                    if (rule.TryGetProperty("id", out var id) && id.GetString() is { } ruleId)
                    {
                        var level = rule.TryGetProperty("defaultConfiguration", out var config) && config.TryGetProperty("level", out var l) ? l.GetString() : null;
                        rules[ruleId] = (level, SecuritySeverity(rule));
                    }
                }
            }

            return rules;
        }

        private static ScanResult Result(JsonElement result, Dictionary<string, (string? Level, double? SecuritySeverity)> rules)
        {
            var ruleId = result.TryGetProperty("ruleId", out var r) ? r.GetString() ?? "" : "";
            var message = result.TryGetProperty("message", out var m) && m.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
            var location = Location(result);
            var rule = rules.GetValueOrDefault(ruleId);
            var level = result.TryGetProperty("level", out var l) ? l.GetString() : rule.Level;
            var severity = Severity(SecuritySeverity(result) ?? rule.SecuritySeverity, level);
            return new ScanResult(Fingerprint(result, ruleId, location, message), ruleId, severity, message, location);
        }

        // R2: "security-severity" first (CVSS-like 0–10), else the SARIF level
        private static FindingSeverity Severity(double? securitySeverity, string? level)
            => securitySeverity switch
            {
                >= 9 => FindingSeverity.Critical,
                >= 7 => FindingSeverity.High,
                >= 4 => FindingSeverity.Medium,
                not null => FindingSeverity.Low,
                null => level switch
                {
                    "error" => FindingSeverity.High,
                    "warning" => FindingSeverity.Medium,
                    _ => FindingSeverity.Low
                }
            };

        private static double? SecuritySeverity(JsonElement element)
            => element.TryGetProperty("properties", out var properties) && properties.TryGetProperty("security-severity", out var value)
               && double.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText(),
                   NumberStyles.Float, CultureInfo.InvariantCulture, out var score)
                ? score
                : null;

        // e.g. "package-lock.json:12"
        private static string? Location(JsonElement result)
        {
            if (!result.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var location in locations.EnumerateArray())
            {
                if (location.TryGetProperty("physicalLocation", out var physical)
                    && physical.TryGetProperty("artifactLocation", out var artifact) && artifact.TryGetProperty("uri", out var uri))
                {
                    var line = physical.TryGetProperty("region", out var region) && region.TryGetProperty("startLine", out var start) ? $":{start.GetInt32()}" : "";
                    return uri.GetString() + line;
                }
            }

            return null;
        }

        // R3: the tool's own fingerprint when it gives one; otherwise rule, location and message together
        private static string Fingerprint(JsonElement result, string ruleId, string? location, string message)
        {
            foreach (var property in new[] { "fingerprints", "partialFingerprints" })
            {
                if (result.TryGetProperty(property, out var prints) && prints.ValueKind == JsonValueKind.Object)
                {
                    foreach (var print in prints.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (print.Value.GetString() is { Length: > 0 } value)
                        {
                            return $"{print.Name}:{value}";
                        }
                    }
                }
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{ruleId}\n{location}\n{message}"));
            return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant()[..32];
        }
    }
}
