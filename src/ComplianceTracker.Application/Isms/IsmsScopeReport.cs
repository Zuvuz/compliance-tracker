using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Application.Isms
{
    // US-208 R1: the ISMS scope and context as a report (ISO/IEC 27001 4.1–4.4), the same model as the other reports, so it
    // is a PDF that can be locked with a fingerprint. Pure, so the content is tested on its own.
    public static class IsmsScopeReport
    {
        public const string Title = "ISMS scope and context";
        public const string Kind = "isms-scope";

        public static ReportDocument Build(IsmsScopeView view, string organisation, string madeBy, DateTime now)
        {
            var scope = view.Scope;
            string Or(string? text) => string.IsNullOrWhiteSpace(text) ? "Not described" : text;
            var version = scope switch
            {
                null or { Version: 0 } => "Not approved yet",
                { ChangedSinceApproval: true } => $"Version {scope.Version} approved; changed since",
                _ => $"Version {scope.Version}, approved {scope.ApprovedAt!.Value.ToLocalTime():dd-MM-yyyy} by {view.ApprovedByName ?? "?"}"
            };

            return new ReportDocument($"{Title} – {organisation}",
            [
                ("Version", version),
                ("Owner", view.OwnerName ?? "No owner"),
                ("Next review", scope?.NextReviewOn is { } due ? $"{due:dd-MM-yyyy}{(view.ReviewOverdue ? " (overdue)" : "")}" : "Not set"),
                ("Made", $"{now.ToLocalTime():dd-MM-yyyy HH:mm} by {madeBy}")
            ],
            [
                new ReportSheet("Scope", ["Part", "Text"], -1,
                [
                    Row("Scope (4.3)", Or(scope?.Text)),
                    Row("Locations", Or(scope?.Locations)),
                    Row("Departments", Or(scope?.Departments)),
                    Row("Internal issues (4.1)", Or(scope?.InternalIssues)),
                    Row("External issues (4.1)", Or(scope?.ExternalIssues))
                ]),
                new ReportSheet("Projects", ["Project", "In the scope", "Reason"], 1, view.Projects.Select(p => new ReportRow(
                    [p.Name, State(p.State), p.Reason ?? ""],
                    p.State switch
                    {
                        ScopeState.Inside => ReportStatus.Good,
                        ScopeState.Outside => ReportStatus.Muted,
                        _ => ReportStatus.Warning
                    })).ToList()),
                new ReportSheet("Interested parties (4.2)", ["Interested party", "Kind", "Requirement", "Handled by the ISMS"], 3,
                    view.Parties.SelectMany(p => p.Requirements.Select(r => new ReportRow(
                        [p.Name, p.Kind.ToString(), r.Text, r.Handled ? "Yes" : "No"], r.Handled ? ReportStatus.Good : ReportStatus.Muted))).ToList())
            ]);
        }

        // R3: a project nobody has decided on is outside until someone puts it inside
        public static string State(ScopeState state) => state switch
        {
            ScopeState.Inside => "Inside",
            ScopeState.Outside => "Outside",
            _ => "Not decided (outside)"
        };

        private static ReportRow Row(string part, string text) => new([part, text], ReportStatus.Normal);
    }
}
