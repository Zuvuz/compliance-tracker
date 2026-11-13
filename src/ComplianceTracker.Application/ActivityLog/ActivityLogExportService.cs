using System.Globalization;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.ActivityLog
{
    public enum ActivityExportFormat
    {
        Csv,
        Pdf
    }

    public record ActivityExportFile(string FileName, string ContentType, byte[] Content, int Entries);

    // US-055: the organisation's activity log for a period, as CSV or PDF, for checking the decisions in other tools.
    // The same people as the log itself (US-102 R6: Admins and Auditors); the export is logged (R5).
    public class ActivityLogExportService
    {
        public const int DefaultDays = 90;
        public const int MaxEntries = 100_000;

        // R1
        public static readonly string[] Columns = ["Time (UTC)", "Action", "Project", "Concerning", "Person", "Role", "Details"];

        private readonly ActivityLogQueries _queries;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPdfWriter _pdf;
        private readonly TimeProvider _clock;

        public ActivityLogExportService(ActivityLogQueries queries, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, IPdfWriter pdf, TimeProvider clock)
        {
            _queries = queries;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _pdf = pdf;
            _clock = clock;
        }

        // Null for a member who may not see the whole log
        public async Task<ActivityExportFile?> ExportAsync(
            ActivityLogQuery query, ActivityProjectScope scope, Guid? projectId, ActivityExportFormat format, CancellationToken cancellationToken = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var period = Period(query, DateOnly.FromDateTime(now.ToLocalTime()));
            var found = await _queries.AllAsync(query with { From = period.From, To = period.To, Page = 1 }, scope, projectId, MaxEntries, cancellationToken);
            if (found is null)
            {
                return null;
            }

            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var rows = Rows(found.Page.Entries, found.Subjects, organization.Users).ToList();
            var projectName = scope switch
            {
                ActivityProjectScope.OneProject => found.Projects.FirstOrDefault(p => p.Id == projectId)?.Name ?? "Unknown project",
                ActivityProjectScope.NotAboutAProject => "Not about a project",
                _ => "All projects"
            };
            var periodText = $"{period.From:dd-MM-yyyy} – {period.To:dd-MM-yyyy}";
            var stem = $"activity-log-{period.From:yyyy-MM-dd}-to-{period.To:yyyy-MM-dd}";

            var file = format == ActivityExportFormat.Pdf
                ? new ActivityExportFile(stem + ".pdf", "application/pdf", _pdf.FromReport(
                    new ReportDocument("Activity log",
                        [("Period", periodText), ("Projects", projectName), ("Entries", rows.Count.ToString(CultureInfo.InvariantCulture))],
                        [new ReportSheet("Entries", Columns, -1, rows.Select(r => new ReportRow(r, ReportStatus.Normal)).ToList())]),
                    new PdfHeader(organization.Name, projectName, "Activity log", now, user.Name)), rows.Count)
                : new ActivityExportFile(stem + ".csv", "text/csv; charset=utf-8", CsvFile.Write(Columns, rows), rows.Count);

            // R5
            await _log.LogAsync(ActivityAction.ActivityLogExported, scope == ActivityProjectScope.OneProject ? projectId : organization.Id,
                $"{format.ToString().ToUpperInvariant()}, {periodText}, {projectName.ToLowerInvariant()} ({rows.Count} {(rows.Count == 1 ? "entry" : "entries")})",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return file;
        }

        // R2: from and to a date, both included; without dates the last 90 days; one date alone keeps the other open to 90 days
        public static (DateOnly From, DateOnly To) Period(ActivityLogQuery query, DateOnly today)
        {
            var to = query.To ?? (query.From is { } from && from.AddDays(DefaultDays) < today ? from.AddDays(DefaultDays) : today);
            var start = query.From ?? to.AddDays(-DefaultDays);
            return start <= to ? (start, to) : (to, start);
        }

        // R1, R3: one row per entry. What the tracker did by itself is "System".
        public static IEnumerable<IReadOnlyList<string>> Rows(
            IEnumerable<ActivityLogEntry> entries, IReadOnlyDictionary<Guid, ActivitySubject> subjects, IEnumerable<User> users)
        {
            var names = users.ToDictionary(u => u.Id, u => u.Name);
            foreach (var entry in entries)
            {
                var subject = entry.SubjectId is { } id && subjects.TryGetValue(id, out var found) ? found : null;
                yield return
                [
                    entry.OccurredAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                    entry.Action.ToString(),
                    subject?.Kind == "Project" ? subject.Name : "",
                    subject is null || subject.Kind == "Project" ? "" : $"{subject.Kind}: {subject.Name}",
                    entry.UserId is { } userId ? names.GetValueOrDefault(userId, "Unknown") : "System",
                    entry.Role is { } role ? RolePermissions.Title(role) : "",
                    entry.Details ?? ""
                ];
            }
        }
    }
}
