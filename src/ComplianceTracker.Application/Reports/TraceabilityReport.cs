using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using System.Globalization;

namespace ComplianceTracker.Application.Reports
{
    // US-120 R2: who the file is for; the content follows the choice
    public enum ReportAudience
    {
        Team,
        Auditor,
        Management
    }

    // US-120 R3: how a row's status is shown (green, red, grey, amber)
    public enum ReportStatus
    {
        Normal,
        Good,
        Bad,
        Muted,
        Warning
    }

    public record ReportRow(IReadOnlyList<string> Cells, ReportStatus Status);

    // StatusColumn: the column that gets the status colour
    public record ReportSheet(string Name, IReadOnlyList<string> Columns, int StatusColumn, IReadOnlyList<ReportRow> Rows);

    public record ReportDocument(string Title, IReadOnlyList<(string Label, string Value)> Summary, IReadOnlyList<ReportSheet> Sheets);

    // US-120: writes a report as a formatted Excel workbook (implemented in Infrastructure with OpenXml)
    public interface ISpreadsheetWriter
    {
        byte[] ToXlsx(ReportDocument document);
    }

    // US-120 R6: what the management summary needs besides the coverage
    public record ManagementFacts(int OpenHighRisks, int OpenSevereFindings, int OpenSecurityEvents, DateOnly? SupportEndsOn);

    // US-120: the traceability matrix (US-052) for one reader, built from the same data as the Compliance tab, so the
    // numbers always agree. The CSV (TraceabilityMatrix) stays as it is for tools that read it.
    public static class TraceabilityReport
    {
        public static string Title(ReportAudience audience) => audience switch
        {
            ReportAudience.Auditor => "Auditor",
            ReportAudience.Management => "Management",
            _ => "Team"
        };

        public static ReportDocument Build(
            ReportAudience audience, Project project, IReadOnlyList<FrameworkCompliance> frameworks, Func<Guid, string> nameOf,
            string downloadedBy, DateTime at, ManagementFacts facts)
        {
            // R3: the Summary sheet
            var summary = new List<(string, string)>
            {
                ("Project", project.Name),
                ("Lifecycle phase", project.Phase.ToString()),
                ("Regulations", string.Join(", ", frameworks.Select(f => $"{FrameworkSlugs.Short(f.Framework.Name)} v{f.Version.Number}"))),
                ("For", Title(audience)),
                ("Downloaded by", $"{downloadedBy}, {at.ToLocalTime():dd-MM-yyyy HH:mm}")
            };

            foreach (var framework in frameworks)
            {
                var name = FrameworkSlugs.Short(framework.Framework.Name);
                summary.Add(($"Coverage – {name}", $"{framework.Percent} % ({framework.Covered} of {framework.Total})"));
                if (audience == ReportAudience.Management)
                {
                    // R6
                    summary.Add(($"Not covered – {name}", framework.NotCovered.ToString(CultureInfo.InvariantCulture)));
                    summary.Add(($"Covered without evidence – {name}",
                        framework.Requirements.Count(r => r.IsApplicable && r.IsCovered && !r.HasEvidence).ToString(CultureInfo.InvariantCulture)));
                }
            }

            if (audience == ReportAudience.Management)
            {
                summary.Add(("Open High and Critical risks", facts.OpenHighRisks.ToString(CultureInfo.InvariantCulture)));
                summary.Add(("Open high and critical scan findings", facts.OpenSevereFindings.ToString(CultureInfo.InvariantCulture)));
                summary.Add(("Open security events", facts.OpenSecurityEvents.ToString(CultureInfo.InvariantCulture)));
                summary.Add(("Supported until", facts.SupportEndsOn?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "(not set)"));
                return new ReportDocument($"Traceability – {project.Name}", summary, []);
            }

            var sheets = frameworks
                .Select(f => audience == ReportAudience.Auditor ? AuditorSheet(f, nameOf) : TeamSheet(f))
                .ToList();
            return new ReportDocument($"Traceability – {project.Name}", summary, sheets);
        }

        // R4: what to do next. Applicable requirements, the not covered first, one row per story, evidence or suggestion.
        private static ReportSheet TeamSheet(FrameworkCompliance framework)
        {
            string[] columns = ["Code", "Requirement", "Status", "Item", "Name", "State", "Details"];
            var rows = new List<ReportRow>();
            foreach (var requirement in framework.Requirements.Where(r => r.IsApplicable).OrderBy(Urgency))
            {
                var (status, style) = Status(requirement);
                string[] start = [requirement.Requirement.Code, requirement.Requirement.Title, status];
                var items = requirement.Links.Select(l => (Item: "Story", Name: l.WorkItem.Title, State: l.WorkItem.State.ToString(),
                        Details: l.WorkItem.ExternalReference + (l.Link.NeedsReview ? " (needs review)" : "")))
                    .Concat(requirement.Evidence.Select(e => (Item: "Evidence", Name: e.Evidence.Title, State: EvidenceState(e),
                        Details: e.Evidence.Url ?? e.Evidence.FileName ?? "")))
                    .Concat(requirement.Suggestions.Select(s => (Item: "AI suggestion", Name: s.WorkItem.Title, State: "Pending",
                        Details: $"score {s.Suggestion.Score.ToString("0.00", CultureInfo.InvariantCulture)}")))
                    .ToList();

                if (items.Count == 0)
                {
                    rows.Add(new ReportRow([.. start, "", "", "", ""], style));
                }

                rows.AddRange(items.Select(i => new ReportRow([.. start, i.Item, i.Name, i.State, i.Details], style)));
            }

            return new ReportSheet(SheetName(framework), columns, 2, rows);
        }

        // R5: the full trace. Every requirement, every link and evidence with who decided, their role and when. No pending
        // AI suggestions, only decisions.
        private static ReportSheet AuditorSheet(FrameworkCompliance framework, Func<Guid, string> nameOf)
        {
            string[] columns =
            [
                "Version", "Code", "Requirement", "Status", "Reason", "Item", "Reference", "Name", "Source",
                "Decided by", "Role", "Decided at", "SHA-256", "Valid until"
            ];
            var version = framework.Version.Number.ToString(CultureInfo.InvariantCulture);
            var rows = new List<ReportRow>();
            foreach (var requirement in framework.Requirements)
            {
                var (status, style) = Status(requirement);
                string[] start = [version, requirement.Requirement.Code, requirement.Requirement.Title, status, requirement.NotApplicableReason ?? ""];
                var items = requirement.Links.Select(l => (string[])
                    [
                        "Story", l.WorkItem.ExternalReference, l.WorkItem.Title, l.Link.Source == Domain.Mappings.LinkSource.AiApproved ? "AI-approved" : "Manual",
                        nameOf(l.Link.LinkedById), Role(l.Link.LinkedRole), Date(l.Link.LinkedAt), "", ""
                    ])
                    .Concat(requirement.Evidence.Select(e => (string[])
                    [
                        "Evidence", e.Evidence.Url ?? e.Evidence.FileName ?? "", e.Evidence.Title, "Manual",
                        nameOf(e.Evidence.AddedById), Role(e.Evidence.AddedRole), Date(e.Evidence.AddedAt), e.Evidence.Sha256 ?? "",
                        e.IsExpired ? "Expired" : e.Evidence.ValidUntil?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? ""
                    ]))
                    .ToList();

                if (items.Count == 0)
                {
                    rows.Add(new ReportRow([.. start, "", "", "", "", "", "", "", "", ""], style));
                }

                rows.AddRange(items.Select(i => new ReportRow([.. start, .. i], style)));
            }

            return new ReportSheet(SheetName(framework), columns, 3, rows);
        }

        // R3: green covered, red not covered, grey not applicable, amber when something is missing or waits
        private static (string Text, ReportStatus Style) Status(RequirementCoverage requirement)
        {
            if (!requirement.IsApplicable)
            {
                return ("Not applicable", ReportStatus.Muted);
            }

            if (requirement.NeedsReview)
            {
                return ("Needs review", ReportStatus.Warning);
            }

            if (!requirement.IsCovered)
            {
                return ("Not covered", ReportStatus.Bad);
            }

            return requirement.HasEvidence ? ("Covered", ReportStatus.Good) : ("Covered, no evidence", ReportStatus.Warning);
        }

        // R4: not covered first, then what needs review or evidence, then the rest, each in the document's order
        private static int Urgency(RequirementCoverage requirement)
            => !requirement.IsCovered ? 0 : requirement.NeedsReview || !requirement.HasEvidence ? 1 : 2;

        private static string EvidenceState(EvidenceItem item)
            => item.IsExpired ? "Expired"
               : item.NeedsReview ? "Needs review"
               : item.Evidence.ValidUntil is { } until ? $"Valid until {until:dd-MM-yyyy}" : "Valid";

        private static string Role(UserRole? role) => role is { } r ? RolePermissions.Title(r) : "";

        private static string Date(DateTime at) => at.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

        // Excel allows 31 characters and none of : \ / ? * [ ]
        private static string SheetName(FrameworkCompliance framework)
        {
            var name = new string(FrameworkSlugs.Short(framework.Framework.Name).Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
            return name.Length == 0 ? "Requirements" : name.Length <= 31 ? name : name[..31];
        }
    }
}
