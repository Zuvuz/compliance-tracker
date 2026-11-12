using System.Globalization;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Reports
{
    // US-053 R1–R4: one control of the Statement of Applicability
    public record SoaControl(string Code, string Title, bool Applicable, IReadOnlyList<string> NotApplicable, string Implementation, IReadOnlyList<string> HowMet);

    // One project that follows the framework, with its coverage of it
    public record SoaProject(string Name, FrameworkCompliance Coverage);

    // US-053: the Statement of Applicability for ISO/IEC 27001 Annex A, across the organisation's projects that follow the
    // organisation's own (uploaded) copy of the standard. ISO's text is not built in (B93). Pure, so the rules are tested.
    public static class StatementOfApplicability
    {
        public const string Title = "Statement of Applicability";
        public const string Implemented = "Implemented";
        public const string Partly = "Partly implemented";
        public const string NotImplemented = "Not implemented";
        public const string NotApplicableText = "Not applicable";
        public static readonly string[] Columns = ["Control", "Title", "Applicable", "Reason", "Implementation", "How it is met"];

        // The organisation's own ISO/IEC 27001, e.g. "ISO/IEC 27001:2022"
        public static bool IsIso27001(Framework framework)
            => !framework.IsBuiltIn && (framework.Name.StartsWith("ISO/IEC 27001", StringComparison.OrdinalIgnoreCase)
                                         || framework.Name.StartsWith("ISO 27001", StringComparison.OrdinalIgnoreCase));

        public static IReadOnlyList<SoaControl> Controls(IReadOnlyList<SoaProject> projects)
        {
            if (projects.Count == 0)
            {
                return [];
            }

            // R1: the controls of the newest version the projects use, in the standard's order; only Annex A when it is there
            var newest = projects.OrderByDescending(p => p.Coverage.Version.Number).First().Coverage.Requirements.Select(r => r.Requirement).ToList();
            var annexA = newest.Where(r => r.Code.StartsWith("A.", StringComparison.OrdinalIgnoreCase)).ToList();
            var controls = annexA.Count > 0 ? annexA : newest;

            return controls.Select(control =>
            {
                var per = projects
                    .Select(p => (p.Name, Coverage: p.Coverage.Requirements.FirstOrDefault(r => string.Equals(r.Requirement.Code, control.Code, StringComparison.OrdinalIgnoreCase))))
                    .Where(p => p.Coverage is not null)
                    .Select(p => (p.Name, Coverage: p.Coverage!))
                    .ToList();

                // R2: not applicable only when every project that has the control marks it so
                var applicableIn = per.Where(p => p.Coverage.IsApplicable).ToList();
                var reasons = per.Where(p => !p.Coverage.IsApplicable).Select(p => $"{p.Name}: {p.Coverage.NotApplicableReason}").ToList();
                if (applicableIn.Count == 0)
                {
                    return new SoaControl(control.Code, control.Title, false, reasons, NotApplicableText, []);
                }

                // R3, R4: worked out from coverage and evidence (B93)
                var implementation = applicableIn.All(p => p.Coverage.IsCovered && p.Coverage.HasEvidence) ? Implemented
                    : applicableIn.Any(p => p.Coverage.IsCovered) ? Partly
                    : NotImplemented;
                var howMet = applicableIn
                    .Where(p => p.Coverage.IsCovered || p.Coverage.Evidence.Count > 0)
                    .Select(p => $"{p.Name}: {string.Join(", ", p.Coverage.Links.Select(l => l.WorkItem.Title).Concat(p.Coverage.Evidence.Where(e => !e.IsExpired).Select(e => e.Evidence.Title)).Distinct())}")
                    .ToList();
                return new SoaControl(control.Code, control.Title, true, reasons, implementation, howMet);
            }).ToList();
        }

        public static ReportDocument Build(Organization organization, Framework framework, IReadOnlyList<SoaProject> projects, string madeBy, DateTime now)
        {
            var controls = Controls(projects);
            int? version = projects.Count == 0 ? null : projects.Max(p => p.Coverage.Version.Number);
            string Count(Func<SoaControl, bool> which) => controls.Count(which).ToString(CultureInfo.InvariantCulture);

            return new ReportDocument($"{Title} – {organization.Name}",
            [
                ("Standard", version is null ? framework.Name : $"{framework.Name}, version {version}"),
                ("Made", $"{now.ToLocalTime():dd-MM-yyyy HH:mm} by {madeBy}"),
                ("Projects", projects.Count == 0 ? "No project follows it" : string.Join(", ", projects.Select(p => p.Name))),
                ("Controls", $"{Count(_ => true)}: {Count(c => c.Applicable)} applicable, {Count(c => !c.Applicable)} not applicable"),
                ("Implementation", $"{Count(c => c.Implementation == Implemented)} implemented, {Count(c => c.Implementation == Partly)} partly, {Count(c => c.Implementation == NotImplemented)} not implemented")
            ],
            [
                new ReportSheet("Controls", Columns, 4, controls.Select(c => new ReportRow(
                [
                    c.Code, c.Title, c.Applicable ? "Yes" : "No", string.Join(" | ", c.NotApplicable), c.Implementation, string.Join(" | ", c.HowMet)
                ], c.Implementation switch
                {
                    Implemented => ReportStatus.Good,
                    Partly => ReportStatus.Warning,
                    NotImplemented => ReportStatus.Bad,
                    _ => ReportStatus.Muted
                })).ToList())
            ]);
        }
    }

    // US-053: finds the organisation's ISO/IEC 27001 and the projects that follow it
    public class StatementOfApplicabilityQueries
    {
        private readonly CurrentMember _member;
        private readonly IFrameworkRepository _frameworks;
        private readonly IProjectRepository _projects;
        private readonly ComplianceQueries _compliance;

        public StatementOfApplicabilityQueries(CurrentMember member, IFrameworkRepository frameworks, IProjectRepository projects, ComplianceQueries compliance)
        {
            _member = member;
            _frameworks = frameworks;
            _projects = projects;
            _compliance = compliance;
        }

        // R6: null when the organisation has no ISO/IEC 27001 framework
        public async Task<(Organization Organization, Framework Framework, IReadOnlyList<SoaProject> Projects)?> ForOrganizationAsync(
            CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || (await _frameworks.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(StatementOfApplicability.IsIso27001) is not { } framework)
            {
                return null;
            }

            var projects = new List<SoaProject>();
            foreach (var project in (await _projects.ListAsync(organization.Id, cancellationToken))
                         .Where(p => p.Frameworks.Any(f => f.FrameworkId == framework.Id))
                         .OrderBy(p => p.IsOrganisationArea ? 0 : 1).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                if ((await _compliance.ForProjectAsync(project.Id, cancellationToken))?.Frameworks.FirstOrDefault(f => f.Framework.Id == framework.Id) is { } coverage)
                {
                    projects.Add(new SoaProject(project.IsOrganisationArea ? "Organisation" : project.Name, coverage));
                }
            }

            return (organization, framework, projects);
        }
    }
}
