using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Specs.Support
{
    // US-078: small SARIF 2.1.0 files for the scenarios, as a scanner like Trivy writes them: the tool, its rules with a
    // "security-severity", and one result per finding with a fingerprint.
    public static class TestScans
    {
        public record Result(string RuleId, double SecuritySeverity, string Message = "A vulnerable package");

        public static byte[] Sarif(string tool, params Result[] results)
        {
            var sarif = new
            {
                version = "2.1.0",
                runs = new[]
                {
                    new
                    {
                        tool = new
                        {
                            driver = new
                            {
                                name = tool,
                                rules = results.Select(r => new
                                {
                                    id = r.RuleId,
                                    properties = new Dictionary<string, object> { ["security-severity"] = r.SecuritySeverity.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                                })
                            }
                        },
                        results = results.Select(r => new
                        {
                            ruleId = r.RuleId,
                            level = "error",
                            message = new { text = r.Message },
                            locations = new[] { new { physicalLocation = new { artifactLocation = new { uri = "package-lock.json" }, region = new { startLine = 12 } } } },
                            partialFingerprints = new Dictionary<string, string> { ["primaryLocationLineHash"] = r.RuleId.ToLowerInvariant() }
                        })
                    }
                }
            };

            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sarif));
        }

        // n findings, the first "critical" of them critical (9.8), the rest high (7.5)
        public static byte[] Findings(string tool, int count, int critical)
            => Sarif(tool, Enumerable.Range(1, count)
                .Select(i => new Result($"CVE-2026-{1000 + i}", i <= critical ? 9.8 : 7.5))
                .ToArray());
    }
}
