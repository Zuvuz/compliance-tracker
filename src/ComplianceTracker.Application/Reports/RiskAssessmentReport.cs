using System.Globalization;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Reports
{
    // US-054: the project's risk assessment as one document (CRA Art. 13(2), ISO 27001 6.1.2 and 8.2): the criteria, the
    // risks by level with their measures and actions, the accepted risks with who accepted them, and the closed ones.
    // Pure, so the rules are tested on their own. Only the current state; the history is in the activity log (US-055).
    public static class RiskAssessmentReport
    {
        public const string Title = "Risk assessment";
        public static readonly string[] Sections = ["Criteria", "Risks", "Accepted risks", "Closed risks"];
        public static readonly string[] RiskColumns =
            ["Risk", "Level", "Likelihood × impact", "Residual", "Status", "Treatment", "Owner", "Measures", "Actions", "Last reviewed", "Next review"];

        // Names a measure's requirement or story, e.g. "CRA I.2(d)" or "[US-004] Sign in"
        public record Names(Func<Guid, string> Person, Func<Guid, string?> Requirement, Func<Guid, string?> Story);

        public static ReportDocument Build(Project project, IEnumerable<Risk> risks, Names names, string madeBy, DateTime now)
        {
            var all = risks.ToList();
            var open = Sorted(all.Where(r => r.Status != RiskStatus.Closed)).ToList();
            var closed = all.Where(r => r.Status == RiskStatus.Closed).OrderByDescending(r => r.ClosedAt).ThenBy(r => r.Title, StringComparer.Ordinal).ToList();

            var summary = new List<(string, string)>
            {
                ("Project", project.Name),
                ("Lifecycle phase", project.Phase.ToString()),
                ("Made", $"{now.ToLocalTime():dd-MM-yyyy HH:mm} by {madeBy}"),
                ("Open risks", open.Count == 0 ? "None" : string.Join(", ", Enum.GetValues<RiskLevel>().Reverse()
                    .Select(l => (Level: l, Count: open.Count(r => r.Level == l))).Where(x => x.Count > 0).Select(x => $"{x.Count} {x.Level}"))),
                ("Review due", Number(open.Count(r => r.IsReviewDue(now))))
            };

            return new ReportDocument($"{Title} – {project.Name}", summary,
            [
                new ReportSheet(Sections[0], ["Likelihood \\ impact", "1", "2", "3", "4", "5"], -1, Criteria()),
                new ReportSheet(Sections[1], RiskColumns, 1, open.Select(r => Row(r, names, now)).ToList()),
                new ReportSheet(Sections[2], ["Risk", "Level", "Accepted by", "Role", "When", "Reason"], 1,
                    open.Where(r => r.Status == RiskStatus.Accepted).Select(r => new ReportRow(
                    [
                        r.Title, Level(r), r.AcceptedById is { } by ? names.Person(by) : "", r.AcceptedRole is { } role ? RolePermissions.Title(role) : "",
                        r.AcceptedAt?.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "", r.AcceptedReason ?? ""
                    ], Style(r.Level))).ToList()),
                new ReportSheet(Sections[3], ["Risk", "Level", "Closed", "Reason"], -1,
                    closed.Select(r => new ReportRow(
                    [
                        r.Title, Level(r), r.ClosedAt?.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "", r.ClosedReason ?? ""
                    ], ReportStatus.Muted)).ToList())
            ]);
        }

        // R2: highest level first, then the highest score, then by title
        public static IEnumerable<Risk> Sorted(IEnumerable<Risk> risks)
            => risks.OrderByDescending(r => r.Level).ThenByDescending(r => r.Score).ThenBy(r => r.Title, StringComparer.Ordinal);

        // R1: the level of each likelihood × impact, likelihood from 5 down, as on the Risks tab (US-042 R8)
        public static IReadOnlyList<ReportRow> Criteria()
            => Enumerable.Range(Risk.MinScale, Risk.MaxScale).Reverse()
                .Select(likelihood => new ReportRow(
                    [likelihood.ToString(CultureInfo.InvariantCulture), .. Enumerable.Range(Risk.MinScale, Risk.MaxScale)
                        .Select(impact => $"{Risk.LevelOf(likelihood * impact)} ({likelihood * impact})")],
                    ReportStatus.Normal))
                .ToList();

        // R3–R5: one open or accepted risk
        public static ReportRow Row(Risk risk, Names names, DateTime now)
        {
            var measures = risk.Measures
                .Select(m => m.RequirementId is { } requirement ? names.Requirement(requirement) : m.WorkItemId is { } story ? names.Story(story) : null)
                .Where(n => n is not null)
                .OrderBy(n => n, StringComparer.Ordinal);
            var actions = risk.Actions.Select(a => $"{a.Title} ({a.Status}{(a.DueOn is { } due && a.Status != RiskActionStatus.Done ? $", due {due:dd-MM-yyyy}" : "")})");
            var reviewDue = risk.IsReviewDue(now);
            return new ReportRow(
            [
                risk.Title,
                Level(risk),
                $"{risk.Likelihood} × {risk.Impact}",
                risk.ResidualLevel is { } residual ? $"{residual} ({risk.ResidualLikelihood} × {risk.ResidualImpact})" : "",
                risk.DisplayStatus,
                risk.Treatment.ToString(),
                names.Person(risk.OwnerId),
                string.Join("; ", measures),
                string.Join("; ", actions),
                risk.LastReviewedAt?.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "Never",
                reviewDue ? "Review due" : NextReview(risk).ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)
            ], reviewDue ? ReportStatus.Warning : Style(risk.Level));
        }

        // US-042 R6: 12 months after the last review, or after the risk was added
        public static DateTime NextReview(Risk risk) => (risk.LastReviewedAt ?? risk.CreatedAt).AddMonths(Risk.ReviewMonths);

        private static string Level(Risk risk) => $"{risk.Level} ({risk.Score})";

        private static ReportStatus Style(RiskLevel level) => level >= RiskLevel.High ? ReportStatus.Bad : level == RiskLevel.Medium ? ReportStatus.Warning : ReportStatus.Normal;

        private static string Number(int count) => count.ToString(CultureInfo.InvariantCulture);
    }
}
