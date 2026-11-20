using System.Text.Json;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;

namespace ComplianceTracker.Application.Security
{
    // US-160 R3, R4: one alert the organisation's monitoring sent: its id (the same while it fires), name, firing or resolved,
    // and severity
    public sealed record MonitoringAlert(string Id, string Name, bool Firing, string Severity, string? Summary);

    // US-160 R4, R6: the two shapes an alert may come in, read without trusting the content. Pure, so it is tested on its own.
    //  - simple: {"alerts":[{"id":"…","name":"HighErrorRate","status":"firing","severity":"critical","summary":"…"}]}
    //  - Prometheus Alertmanager's webhook: {"alerts":[{"status":"firing","labels":{"alertname":"…","severity":"…"},
    //    "annotations":{"summary":"…"},"fingerprint":"…"}]}
    public static class MonitoringAlertParser
    {
        public const int MaxBytes = 256 * 1024;

        // Null when the body is not one of the shapes
        public static IReadOnlyList<MonitoringAlert>? Parse(byte[] body)
        {
            try
            {
                using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("alerts", out var alerts) || alerts.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var result = new List<MonitoringAlert>();
                foreach (var alert in alerts.EnumerateArray())
                {
                    if (alert.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    var labels = alert.TryGetProperty("labels", out var l) && l.ValueKind == JsonValueKind.Object ? l : (JsonElement?)null;
                    var annotations = alert.TryGetProperty("annotations", out var a) && a.ValueKind == JsonValueKind.Object ? a : (JsonElement?)null;
                    var name = Text(labels, "alertname") ?? Text(alert, "name");
                    var status = Text(alert, "status");
                    if (string.IsNullOrWhiteSpace(name) || status is not ("firing" or "resolved"))
                    {
                        return null;
                    }

                    var severity = (Text(labels, "severity") ?? Text(alert, "severity") ?? "").ToLowerInvariant();
                    var id = Text(alert, "fingerprint") ?? Text(alert, "id") ?? name;
                    var summary = Text(annotations, "summary") ?? Text(alert, "summary");
                    result.Add(new MonitoringAlert(Cut(id, 100), Cut(name, 150), status == "firing", severity,
                        string.IsNullOrWhiteSpace(summary) ? null : Cut(summary, 300)));
                }

                return result;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // R4: only critical and high fire a suggestion
        public static bool Serious(MonitoringAlert alert) => alert.Severity is "critical" or "high";

        private static string? Text(JsonElement? element, string name)
            => element is { } e && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }

    public enum AlertStatus
    {
        Accepted,
        Unauthorized,
        TooLarge,
        Invalid
    }

    public sealed record AlertResult(AlertStatus Status, string Message, int Suggested = 0, int Closed = 0);

    // US-160 R3–R6: alerts from the organisation's monitoring, with the project's upload token (US-122). Nothing becomes a
    // security event by itself; a person records or dismisses each suggestion (US-128), and recording starts the CRA
    // Art. 14 clock from when the alert came in.
    public class MonitoringAlertService
    {
        private const string NotValid = "The token is not valid for this project";

        private readonly IScanUploadTokenRepository _tokens;
        private readonly IProjectRepository _projects;
        private readonly ISecurityEventSuggestionRepository _suggestions;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public MonitoringAlertService(IScanUploadTokenRepository tokens, IProjectRepository projects, ISecurityEventSuggestionRepository suggestions,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _tokens = tokens;
            _projects = projects;
            _suggestions = suggestions;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<AlertResult> ReceiveAsync(Guid projectId, string? secret, byte[] body, CancellationToken cancellationToken = default)
        {
            // R3: a missing, wrong, revoked or other project's token is refused, and nothing is stored
            if (string.IsNullOrWhiteSpace(secret) || !secret.StartsWith(ScanUploadToken.Prefix, StringComparison.Ordinal)
                || await _tokens.FindByHashAsync(ScanUploadToken.HashOf(secret.Trim()), cancellationToken) is not { } token
                || !token.Allows(projectId)
                || await _projects.GetForSyncAsync(projectId, cancellationToken) is not { } project)
            {
                return new AlertResult(AlertStatus.Unauthorized, NotValid);
            }

            var now = _clock.GetUtcNow().UtcDateTime;

            // R6: refused and logged without the content
            if (body.Length > MonitoringAlertParser.MaxBytes)
            {
                await RefuseAsync(project, $"Alert refused: larger than 256 KB ({body.Length} bytes)", cancellationToken);
                return new AlertResult(AlertStatus.TooLarge, "The alert is larger than 256 KB");
            }

            if (MonitoringAlertParser.Parse(body) is not { } alerts)
            {
                await RefuseAsync(project, $"Alert refused: not a known shape ({body.Length} bytes)", cancellationToken);
                return new AlertResult(AlertStatus.Invalid, "Send {\"alerts\":[…]} in the simple shape or Alertmanager's webhook");
            }

            token.Used(now);
            if (project.IsClosed)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return new AlertResult(AlertStatus.Accepted, "The project is closed; nothing is suggested");
            }

            var existing = await _suggestions.ListAsync(project.Id, cancellationToken);
            var (added, closed) = Apply(project.Id, alerts, existing, now);
            if (added.Count > 0)
            {
                await _suggestions.AddAsync(added, cancellationToken);
            }

            foreach (var suggestion in added)
            {
                await _log.LogSystemAsync(project.OrganizationId, ActivityAction.SecurityEventSuggested, project.Id,
                    $"{suggestion.Title} (Monitoring)", cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new AlertResult(AlertStatus.Accepted, $"{added.Count} suggested, {closed} closed", added.Count, closed);
        }

        // R4: a firing serious alert is suggested once while it is open; "resolved" closes a suggestion nobody has decided on
        public static (IReadOnlyList<SecurityEventSuggestion> Added, int Closed) Apply(Guid projectId, IReadOnlyList<MonitoringAlert> alerts,
            IReadOnlyList<SecurityEventSuggestion> existing, DateTime now)
        {
            var added = new List<SecurityEventSuggestion>();
            var closed = 0;
            foreach (var alert in alerts)
            {
                var key = $"monitoring:{alert.Id}";
                var pending = existing.Concat(added).FirstOrDefault(s => s.Key == key && s.IsPending);
                if (!alert.Firing)
                {
                    if (pending is not null && existing.Contains(pending))
                    {
                        pending.Supersede("Resolved at the source", now);
                        closed++;
                    }
                    else if (pending is not null)
                    {
                        added.Remove(pending);
                    }

                    continue;
                }

                if (pending is null && MonitoringAlertParser.Serious(alert))
                {
                    added.Add(SecurityEventSuggestion.Create(projectId, key, alert.Summary is { } summary ? $"{alert.Name}: {summary}" : alert.Name,
                        SuggestionSource.Monitoring, "Monitoring", SecurityEventKind.IncidentNotSevere, null, [], now));
                }
            }

            return (added, closed);
        }

        private async Task RefuseAsync(Domain.Projects.Project project, string text, CancellationToken cancellationToken)
        {
            await _log.LogSystemAsync(project.OrganizationId, ActivityAction.SecurityAlertRefused, project.Id, text, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
