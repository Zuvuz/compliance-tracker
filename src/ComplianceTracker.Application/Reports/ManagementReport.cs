using System.Globalization;
using System.Text.Json;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Reports
{
    // US-141 R6: an open High or Critical risk as the management report lists it. HasOwner is false when the owner is no
    // longer a member of the organisation.
    public record ManagementRisk(string Title, RiskLevel Level, int Score, string Owner, bool HasOwner, DateOnly? DueOn);

    // US-141 R3: the newest locked management report with the same regulations, and its coverage per regulation
    public record LastManagementReport(DateTime LockedAt, IReadOnlyDictionary<string, int> Percents);

    // US-141: what the management report shows, most important first. Pure functions, so the rules are tested on their own.
    public static class ManagementPriorities
    {
        public const int MaxPoints = 5;
        public const int MaxGaps = 10;
        public const string NothingToDecide = "Nothing needs a decision right now";
        public const string NoLockedReport = "No locked report to compare with";

        // The sections in their fixed order (R1); each is a sheet in Excel and a heading in the PDF (R8)
        public static readonly IReadOnlyList<string> Sections =
            ["Status", "What management must know now", "Key figures", "Biggest gaps", "Open high risks", "Details"];

        // R2: red under 50 %, yellow 50–79 %, green 80 % or more, on the rounded per cent shown; no colour without requirements
        public static (string Text, ReportStatus Style) Light(int covered, int total)
        {
            if (total <= 0)
            {
                return ("No requirements yet", ReportStatus.Muted);
            }

            var percent = Percent(covered, total);
            return percent < 50 ? ("Red", ReportStatus.Bad) : percent < 80 ? ("Yellow", ReportStatus.Warning) : ("Green", ReportStatus.Good);
        }

        // The same rounding as the Compliance tab (US-021 R4)
        public static int Percent(int covered, int total)
            => total <= 0 ? 0 : (int)Math.Round(100.0 * Math.Clamp(covered, 0, total) / total, MidpointRounding.AwayFromZero);

        // R3: e.g. "↑ +3 since 01-11-2026"
        public static string SinceLast(string regulation, int percent, LastManagementReport? last)
        {
            if (last is null || !last.Percents.TryGetValue(regulation, out var before))
            {
                return NoLockedReport;
            }

            var date = last.LockedAt.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
            var change = percent - before;
            return change switch
            {
                > 0 => $"↑ +{change} since {date}",
                < 0 => $"↓ {change} since {date}",
                _ => $"= unchanged since {date}"
            };
        }

        // R3: the numbers a locked management report keeps, so a later report can compare without reading the old PDF
        public static IReadOnlyDictionary<string, int> Figures(IEnumerable<FrameworkCompliance> frameworks)
            => frameworks.GroupBy(f => f.Framework.Name).ToDictionary(g => g.Key, g => g.First().Percent);

        public static string FiguresJson(IReadOnlyDictionary<string, int> figures) => JsonSerializer.Serialize(figures);

        // Unreadable or missing figures are treated as no figures, never as an error
        public static IReadOnlyDictionary<string, int>? ParseFigures(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // R3: "the same regulations" means the same set of framework names, in any order
        public static bool SameRegulations(IReadOnlyDictionary<string, int> figures, IEnumerable<string> regulations)
            => figures.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(regulations);

        // R4: at most five points, most serious first; "Nothing needs a decision right now" when there are none
        public static IReadOnlyList<string> Points(IReadOnlyList<FrameworkCompliance> frameworks, ManagementFacts facts)
        {
            var points = new List<string>();
            if (facts.OpenSecurityEvents > 0)
            {
                points.Add($"{Count(facts.OpenSecurityEvents, "open security event")} to handle; the CRA has reporting deadlines (Art. 14)");
            }

            if (facts.OpenSevereFindings > 0)
            {
                points.Add($"{Count(facts.OpenSevereFindings, "open high or critical scan finding")}");
            }

            var withoutOwner = facts.HighRisks.Count(r => !r.HasOwner);
            if (withoutOwner > 0)
            {
                points.Add($"{Count(withoutOwner, "High or Critical risk")} without an owner");
            }

            if (frameworks.Any(f => IsCra(f.Framework.Name)) && facts.SupportEndsOn is null)
            {
                points.Add("Support period not set; CRA requires one (Art. 13(8))");
            }

            var measured = frameworks.Where(f => f.Total > 0).OrderBy(f => f.Percent).ThenBy(f => f.Framework.Name, StringComparer.Ordinal).ToList();
            points.AddRange(measured.Where(f => Light(f.Covered, f.Total).Style == ReportStatus.Bad)
                .Select(f => $"{FrameworkSlugs.Short(f.Framework.Name)}: {f.NotCovered} of {f.Total} requirements not covered ({f.Percent} %)"));

            if (facts.OpenHighRisks > 0)
            {
                points.Add($"{Count(facts.OpenHighRisks, "open High or Critical risk")}");
            }

            points.AddRange(measured.Where(f => Light(f.Covered, f.Total).Style == ReportStatus.Warning)
                .Select(f => $"{FrameworkSlugs.Short(f.Framework.Name)}: {f.NotCovered} of {f.Total} requirements not covered ({f.Percent} %)"));

            return points.Count == 0 ? [NothingToDecide] : points.Take(MaxPoints).ToList();
        }

        // R5: at most ten not covered requirements, (a) linked to an open High or Critical risk, highest first, (b) with a
        // deadline, (c) the rest; each group in the catalogue's order
        public static IReadOnlyList<(RequirementCoverage Requirement, string Why)> Gaps(FrameworkCompliance framework)
        {
            var cra = IsCra(framework.Framework.Name);
            return framework.Requirements
                .Select((requirement, index) => (requirement, index))
                .Where(x => x.requirement.IsApplicable && !x.requirement.IsCovered)
                .Select(x =>
                {
                    var risk = x.requirement.Risks.Where(r => r.Level >= RiskLevel.High).Select(r => (RiskLevel?)r.Level).Max();
                    var deadline = cra && HasDeadline(x.requirement.Requirement.Code);
                    var (group, why) = risk is { } level ? (level == RiskLevel.Critical ? 1 : 2, $"Linked to a {level} risk")
                        : deadline ? (3, "Has a deadline (CRA Art. 14)")
                        : (4, "Not covered");
                    return (x.requirement, x.index, group, why);
                })
                .OrderBy(x => x.group)
                .ThenBy(x => x.index)
                .Take(MaxGaps)
                .Select(x => (x.requirement, x.why))
                .ToList();
        }

        // R6: highest first (level, then score), then by title
        public static IReadOnlyList<ManagementRisk> Risks(IEnumerable<ManagementRisk> risks)
            => risks.Where(r => r.Level >= RiskLevel.High)
                .OrderByDescending(r => r.Level).ThenByDescending(r => r.Score).ThenBy(r => r.Title, StringComparer.Ordinal)
                .ToList();

        // B77: the CRA's reporting obligations (Art. 14) have deadlines of 24 hours, 72 hours and 14 days
        public static bool HasDeadline(string code) => code.Trim().StartsWith("Art. 14", StringComparison.OrdinalIgnoreCase);

        public static bool IsCra(string frameworkName) => frameworkName.Contains("2024/2847", StringComparison.Ordinal);

        private static string Count(int n, string thing) => $"{n} {thing}{(n == 1 ? "" : "s")}";

        // ---------------------------------------------------------- The sections

        public static IReadOnlyList<ReportSheet> Sheets(IReadOnlyList<FrameworkCompliance> frameworks, ManagementFacts facts)
        {
            var status = frameworks.Select(f =>
            {
                var (light, style) = Light(f.Covered, f.Total);
                var coverage = f.Total == 0 ? "No requirements yet" : $"{f.Percent} % ({f.Covered} of {f.Total})";
                return new ReportRow([FrameworkSlugs.Short(f.Framework.Name), light, coverage, SinceLast(f.Framework.Name, f.Percent, facts.Last)], style);
            }).ToList();

            var points = Points(frameworks, facts).Select(p => new ReportRow([p], ReportStatus.Normal)).ToList();

            var covered = frameworks.Sum(f => f.Covered);
            var total = frameworks.Sum(f => f.Total);
            var withoutEvidence = frameworks.Sum(WithoutEvidence);
            string Number(int n) => n.ToString(CultureInfo.InvariantCulture);
            List<ReportRow> figures =
            [
                new(["Requirements covered", $"{covered} of {total}"], ReportStatus.Normal),
                new(["Not covered", Number(total - covered)], total - covered > 0 ? ReportStatus.Bad : ReportStatus.Good),
                new(["Covered without evidence", Number(withoutEvidence)], withoutEvidence > 0 ? ReportStatus.Warning : ReportStatus.Normal),
                new(["Open High and Critical risks", Number(facts.OpenHighRisks)], facts.OpenHighRisks > 0 ? ReportStatus.Bad : ReportStatus.Normal),
                new(["Open severe scan findings", Number(facts.OpenSevereFindings)], facts.OpenSevereFindings > 0 ? ReportStatus.Bad : ReportStatus.Normal),
                new(["Open security events", Number(facts.OpenSecurityEvents)], facts.OpenSecurityEvents > 0 ? ReportStatus.Bad : ReportStatus.Normal),
                new(["Supported until", facts.SupportEndsOn?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "(not set)"],
                    facts.SupportEndsOn is null ? ReportStatus.Warning : ReportStatus.Normal)
            ];

            var gaps = frameworks.SelectMany(f => Gaps(f).Select(g => new ReportRow(
                [FrameworkSlugs.Short(f.Framework.Name), g.Requirement.Requirement.Code, g.Requirement.Requirement.Title, g.Why],
                g.Why.StartsWith("Linked", StringComparison.Ordinal) ? ReportStatus.Bad : ReportStatus.Warning))).ToList();

            var risks = Risks(facts.HighRisks).Select(r => new ReportRow(
                [r.Title, r.Level.ToString(), r.HasOwner ? r.Owner : "(no owner)", r.DueOn?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? ""],
                r.Level >= RiskLevel.Critical ? ReportStatus.Bad : ReportStatus.Warning)).ToList();

            var details = frameworks.Select(f => new ReportRow(
            [
                FrameworkSlugs.Short(f.Framework.Name), Number(f.Covered), Number(f.NotCovered), Number(WithoutEvidence(f)), Number(f.NotApplicable),
                f.Total == 0 ? "No requirements yet" : $"{f.Percent} %"
            ], Light(f.Covered, f.Total).Style)).ToList();

            return
            [
                new ReportSheet(Sections[0], ["Regulation", "Status", "Coverage", "Since last"], 1, status),
                new ReportSheet(Sections[1], ["Point"], -1, points) { Kind = ReportSheetKind.Points },
                new ReportSheet(Sections[2], ["Figure", "Value"], 1, figures) { Kind = ReportSheetKind.Tiles },
                new ReportSheet(Sections[3], ["Regulation", "Code", "Requirement", "Why"], 3, gaps),
                new ReportSheet(Sections[4], ["Risk", "Level", "Owner", "Due"], 1, risks),
                new ReportSheet(Sections[5], ["Regulation", "Covered", "Not covered", "Covered without evidence", "Not applicable", "Coverage"], 5, details)
            ];
        }

        private static int WithoutEvidence(FrameworkCompliance framework)
            => framework.Requirements.Count(r => r.IsApplicable && r.IsCovered && !r.HasEvidence);
    }
}
