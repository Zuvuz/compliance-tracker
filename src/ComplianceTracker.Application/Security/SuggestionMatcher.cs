using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Application.Security
{
    // US-128: CVE ids in a finding's rule id or message, e.g. Trivy's "CVE-2026-1234"; upper case, without duplicates
    public static partial class CveIds
    {
        public static IReadOnlyList<string> In(params string?[] texts)
            => texts.Where(t => !string.IsNullOrEmpty(t))
                .SelectMany(t => Pattern().Matches(t!).Select(m => m.Value.ToUpperInvariant()))
                .Distinct()
                .ToList();

        [GeneratedRegex(@"\bCVE-\d{4}-\d{4,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        private static partial Regex Pattern();
    }

    // US-128 R2–R4, R6: which suggestions a project's findings give. Pure, so the rules are unit tested on their own.
    public static class SuggestionMatcher
    {
        // exploited: CVE id → the name of the first switched-on list it is on.
        // newFindingIds: the findings the last scan added (R3: only a new critical finding suggests "not exploited").
        // Returns the new suggestions; pending "not exploited" suggestions for a CVE that is now exploited are superseded.
        public static IReadOnlyList<SecurityEventSuggestion> Match(
            Guid projectId, IReadOnlyList<Finding> findings, IReadOnlySet<Guid> newFindingIds,
            IReadOnlyDictionary<string, string> exploited, IReadOnlyList<SecurityEvent> events,
            IReadOnlyList<SecurityEventSuggestion> suggestions, DateTime now)
        {
            var open = findings.Where(f => f.IsOpen).ToList();
            var eventsByCve = events
                .SelectMany(e => CveIds.In(e.Title, e.Description).Select(cve => (cve, e)))
                .ToLookup(x => x.cve, x => x.e);
            var added = new List<SecurityEventSuggestion>();

            // An upgrade that was done another way, or an event that was closed, needs no suggestion any more
            foreach (var stale in suggestions.Where(s => s.IsPending && s.UpgradeEventId is { } id
                                                         && !events.Any(e => e.Id == id && e.IsOpen && e.Kind == SecurityEventKind.VulnerabilityNotExploited)))
            {
                stale.Supersede("The event is upgraded or closed already", now);
            }

            var byCve = open
                .SelectMany(f => CveIds.In(f.RuleId, f.Message).Select(cve => (cve, f)))
                .GroupBy(x => x.cve, x => x.f)
                .OrderBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in byCve)
            {
                var cve = group.Key;
                var ids = group.Select(f => f.Id).ToList();
                var known = eventsByCve[cve].ToList();

                if (exploited.TryGetValue(cve, out var list))
                {
                    // R4: an event that is reportable already, or closed, needs nothing new
                    var notExploited = known.FirstOrDefault(e => e.IsOpen && e.Kind == SecurityEventKind.VulnerabilityNotExploited);
                    if (known.Count > 0 && notExploited is null)
                    {
                        continue;
                    }

                    if (Blocked(suggestions, cve, SecurityEventKind.ActivelyExploitedVulnerability, notExploited?.Id, ids))
                    {
                        continue;
                    }

                    foreach (var stale in suggestions.Where(s => s.IsPending && s.Key == cve && s.Kind == SecurityEventKind.VulnerabilityNotExploited))
                    {
                        stale.Supersede("Now actively exploited", now);
                    }

                    added.Add(SecurityEventSuggestion.Create(
                        projectId, cve, notExploited?.Title ?? cve, SuggestionSource.ExploitedList, list,
                        SecurityEventKind.ActivelyExploitedVulnerability, notExploited?.Id, ids, now));
                }
                else if (group.FirstOrDefault(f => Serious(f) && newFindingIds.Contains(f.Id)) is { } critical
                         && known.Count == 0
                         && !Blocked(suggestions, cve, SecurityEventKind.VulnerabilityNotExploited, null, ids))
                {
                    // R3; US-160 R1: a GitHub alert says where it came from, and a Dependabot alert names the package
                    var github = GitHubAlertsSarif.SourceOf(critical.Fingerprint);
                    added.Add(SecurityEventSuggestion.Create(
                        projectId, cve, github == "Dependabot" && PackageOf(critical.Message) is { } package ? $"Vulnerability in {package} ({cve})" : cve,
                        github is null ? SuggestionSource.CriticalFinding : SuggestionSource.GitHubAlert, github ?? critical.Tool,
                        SecurityEventKind.VulnerabilityNotExploited, null, ids, now));
                }
            }

            // R3: a new critical finding without a CVE, e.g. a CodeQL rule. US-160 R2: a leaked secret is an incident.
            foreach (var finding in open.Where(f => Serious(f) && newFindingIds.Contains(f.Id) && CveIds.In(f.RuleId, f.Message).Count == 0))
            {
                var github = GitHubAlertsSarif.SourceOf(finding.Fingerprint);
                var secret = github == "Secret scanning";
                var kind = secret ? SecurityEventKind.IncidentNotSevere : SecurityEventKind.VulnerabilityNotExploited;
                var key = $"finding:{finding.Id}";
                if (!Blocked(suggestions, key, kind, null, [finding.Id]))
                {
                    added.Add(SecurityEventSuggestion.Create(
                        projectId, key, secret ? $"Leaked secret: {finding.Message}" : $"{finding.RuleId}: {finding.Message}",
                        github is null ? SuggestionSource.CriticalFinding : SuggestionSource.GitHubAlert, github ?? finding.Tool,
                        kind, null, [finding.Id], now));
                }
            }

            return added;
        }

        // R3: critical; US-160 R1: from GitHub also high
        public static bool Serious(Finding finding)
            => finding.Severity == FindingSeverity.Critical
               || (finding.Severity == FindingSeverity.High && GitHubAlertsSarif.SourceOf(finding.Fingerprint) is not null);

        // US-160: the package of a Dependabot alert's message, "… (npm: log-lib)" → "log-lib"
        public static string? PackageOf(string? message)
        {
            var text = (message ?? "").TrimEnd();
            var open = text.LastIndexOf('(');
            if (open < 0 || !text.EndsWith(')'))
            {
                return null;
            }

            var inside = text[(open + 1)..^1];
            var colon = inside.IndexOf(':');
            var name = (colon >= 0 ? inside[(colon + 1)..] : inside).Trim();
            return name.Length == 0 ? null : name;
        }

        // R2 "once per CVE"; R6: a dismissed suggestion comes back only for a finding it was not based on
        private static bool Blocked(
            IReadOnlyList<SecurityEventSuggestion> suggestions, string key, SecurityEventKind kind, Guid? upgradeEventId,
            IReadOnlyList<Guid> findingIds)
        {
            var same = suggestions.Where(s => s.Key == key && s.Kind == kind && s.UpgradeEventId == upgradeEventId).ToList();
            if (same.Any(s => s.Status != SuggestionStatus.Dismissed))
            {
                return true;
            }

            var dismissed = same.SelectMany(s => s.FindingIds).ToHashSet();
            return same.Count > 0 && findingIds.All(dismissed.Contains);
        }
    }
}
