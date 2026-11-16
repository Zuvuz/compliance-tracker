using System.Text;
using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Reports
{
    // US-178: where a missing item is fixed
    public enum ReadinessFix
    {
        Requirement,   // the requirement's page (Code)
        Compliance,    // the framework's overview
        Risks,
        Scans,         // the SBOM
        ProjectEdit,   // support period, vulnerability contact
        Audits,
        Overview,      // US-194: the project's lifecycle checklist
        Evidence       // US-195: the Evidence tab
    }

    // One line of the check: what an auditor asks for, whether it is there, and where it is fixed
    public record ReadinessLine(string Title, bool Ok, string Detail, ReadinessFix Fix, string? Code = null);

    public record FrameworkReadiness(string Framework, string Slug, IReadOnlyList<ReadinessLine> Lines)
    {
        public int Missing => Lines.Count(l => !l.Ok);
    }

    // R1: the whole check; "Ready" when nothing is missing
    public record PackageReadiness(IReadOnlyList<FrameworkReadiness> Frameworks)
    {
        public int Missing => Frameworks.Sum(f => f.Missing);
        public bool IsReady => Missing == 0;
        public string Summary => IsReady ? "Ready" : $"{Missing} {(Missing == 1 ? "thing is" : "things are")} missing";
    }

    // What the check knows about the project besides its requirements
    public record ReadinessFacts(bool HasSbom, DateOnly? SupportEndsOn, string? VulnerabilityContact, int Risks, DateOnly? LastAudit);

    // US-178: what an auditor usually asks for, per framework, checked against what the tracker has. Pure, so the edge
    // cases are tested on their own.
    public static class AuditReadiness
    {
        // R4: evidence that ends within this many days is flagged, so it can be renewed before the audit
        public const int ExpiresSoonDays = 60;

        // R1: the clauses of ISO/IEC 27001 an auditor asks to see, by their code in the organisation's copy
        public static readonly IReadOnlyList<(string Code, string Title)> IsoKeyClauses =
        [
            ("4.3", "Scope of the ISMS"),
            ("5.2", "Information security policy"),
            ("6.1.2", "Information security risk assessment"),
            ("6.1.3", "Risk treatment and the Statement of Applicability"),
            ("9.2", "Internal audit"),
            ("9.3", "Management review")
        ];

        // How many requirements without evidence are named one by one; the rest are counted
        public const int NamedRequirements = 10;

        public static bool IsIso27001(Framework framework) => framework.Name.Contains("27001", StringComparison.Ordinal);

        // US-195 R5: evidence without an end date never reminds anyone
        public static FrameworkReadiness Dates(int withoutDate)
            => new("Review dates", "review-dates",
            [
                new ReadinessLine("Evidence has an end date", withoutDate == 0,
                    withoutDate == 0 ? "Every piece of evidence has one" : $"{withoutDate} {(withoutDate == 1 ? "piece has" : "pieces have")} no end date",
                    ReadinessFix.Evidence)
            ]);

        // US-194 R5: the current phase's checklist as one more group, e.g. "Lifecycle: 2 items missing in Verification"
        public static FrameworkReadiness Lifecycle(Projects.ProjectLifecycle lifecycle)
        {
            var missing = lifecycle.Missing;
            return new FrameworkReadiness("Lifecycle", "lifecycle",
            [
                new ReadinessLine($"Checklist of the phase {lifecycle.Phase}", missing.Count == 0,
                    missing.Count == 0 ? $"{lifecycle.Done} of {lifecycle.Counted} done"
                        : $"{missing.Count} missing: {string.Join(", ", missing.Select(m => m.Item.Text))}",
                    ReadinessFix.Overview)
            ]);
        }

        public static PackageReadiness Check(IReadOnlyList<FrameworkCompliance> frameworks, ReadinessFacts facts, DateOnly today)
            => new(frameworks.Select(f => Check(f, facts, today)).ToList());

        public static FrameworkReadiness Check(FrameworkCompliance framework, ReadinessFacts facts, DateOnly today)
        {
            var lines = new List<ReadinessLine>();
            var rows = framework.Requirements.Where(r => !r.Requirement.IsRecommendation).ToList();

            if (IsIso27001(framework.Framework))
            {
                foreach (var (code, title) in IsoKeyClauses)
                {
                    var row = rows.FirstOrDefault(r => string.Equals(r.Requirement.Code, code, StringComparison.OrdinalIgnoreCase));
                    if (row is null)
                    {
                        continue; // the organisation's copy does not have this clause; the general check still counts the others
                    }

                    var audited = code == "9.2" && facts.LastAudit is { } held && held >= today.AddMonths(-12);
                    var ok = !row.IsApplicable || HasValidEvidence(row) || audited;
                    lines.Add(new ReadinessLine($"{title} ({code})", ok,
                        !row.IsApplicable ? $"Not applicable: {row.NotApplicableReason}"
                        : audited ? $"Internal audit held on {facts.LastAudit:dd-MM-yyyy}"
                        : ok ? "Evidence is there" : "No valid evidence",
                        code == "9.2" && !ok ? ReadinessFix.Audits : ReadinessFix.Requirement, row.Requirement.Code));
                }
            }

            if (TechnicalDocumentationQueries.IsCra(framework.Framework))
            {
                lines.Add(new ReadinessLine("Software bill of materials (Annex I, Part II (1))", facts.HasSbom,
                    facts.HasSbom ? "An SBOM is uploaded" : "No SBOM uploaded", ReadinessFix.Scans));
                lines.Add(new ReadinessLine("Support period (Art. 13(8))", facts.SupportEndsOn is not null,
                    facts.SupportEndsOn is { } ends ? $"Support until {ends:dd-MM-yyyy}" : "No support period set", ReadinessFix.ProjectEdit));
                lines.Add(new ReadinessLine("Contact for vulnerabilities (Annex II.2)", !string.IsNullOrWhiteSpace(facts.VulnerabilityContact),
                    string.IsNullOrWhiteSpace(facts.VulnerabilityContact) ? "No contact set" : facts.VulnerabilityContact!, ReadinessFix.ProjectEdit));
                lines.Add(new ReadinessLine("Cybersecurity risk assessment (Art. 13(2))", facts.Risks > 0,
                    facts.Risks > 0 ? $"{facts.Risks} {(facts.Risks == 1 ? "risk" : "risks")} in the register" : "The risk register is empty", ReadinessFix.Risks));
            }

            // R1, R5: every applicable requirement needs valid evidence; not applicable ones are not missing
            var without = rows.Where(r => r.IsApplicable && !HasValidEvidence(r)).ToList();
            lines.Add(new ReadinessLine("Evidence for every applicable requirement", without.Count == 0,
                without.Count == 0
                    ? $"All {rows.Count(r => r.IsApplicable)} applicable requirements have valid evidence"
                    : $"{without.Count} without valid evidence: " + string.Join(", ", without.Take(NamedRequirements).Select(r => r.Requirement.Code))
                      + (without.Count > NamedRequirements ? $" and {without.Count - NamedRequirements} more" : ""),
                ReadinessFix.Compliance));

            // R4: evidence that is still valid but ends soon
            var soon = today.AddDays(ExpiresSoonDays);
            foreach (var (row, item) in rows.Where(r => r.IsApplicable)
                         .SelectMany(r => r.Evidence.Where(e => !e.IsExpired && e.Evidence.ValidUntil is { } until && until <= soon).Select(e => (r, e)))
                         .DistinctBy(x => (x.e.Evidence.Id, x.r.Requirement.Id)))
            {
                lines.Add(new ReadinessLine($"\"{item.Evidence.Title}\" on {row.Requirement.Code}", false,
                    $"Valid until {item.Evidence.ValidUntil:dd-MM-yyyy}; renew it before the audit", ReadinessFix.Requirement, row.Requirement.Code));
            }

            // Links and decisions carried over to a new version that nobody has confirmed (US-110)
            var review = rows.Count(r => r.NeedsReview);
            if (review > 0)
            {
                lines.Add(new ReadinessLine("Changes waiting for review", false,
                    $"{review} {(review == 1 ? "requirement" : "requirements")} changed in the new version and wait for review", ReadinessFix.Compliance));
            }

            return new FrameworkReadiness(FrameworkSlugs.Short(framework.Framework.Name), FrameworkSlugs.Slug(framework.Framework.Name), lines);
        }

        // R3: the check as the package's 00-check.txt
        public static string Text(PackageReadiness readiness, DateOnly today)
        {
            var text = new StringBuilder();
            text.AppendLine($"Audit package check, {today:dd-MM-yyyy}: {readiness.Summary}");
            foreach (var framework in readiness.Frameworks)
            {
                text.AppendLine();
                text.AppendLine($"{framework.Framework}: {(framework.Missing == 0 ? "ready" : $"{framework.Missing} missing")}");
                foreach (var line in framework.Lines)
                {
                    text.AppendLine($"  [{(line.Ok ? "x" : " ")}] {line.Title}: {line.Detail}");
                }
            }

            return text.ToString();
        }

        private static bool HasValidEvidence(RequirementCoverage row) => row.Evidence.Any(e => !e.IsExpired);
    }

    // US-178: the check for a project, with the facts it needs
    public class AuditReadinessService
    {
        private readonly ProjectQueries _projects;
        private readonly ComplianceQueries _compliance;
        private readonly ISbomRepository _sboms;
        private readonly IRiskRepository _risks;
        private readonly AuditService _audits;
        private readonly TimeProvider _clock;
        private readonly Projects.PhaseGates _gates; // US-194
        private readonly IEvidenceRepository _evidence; // US-195

        public AuditReadinessService(ProjectQueries projects, ComplianceQueries compliance, ISbomRepository sboms, IRiskRepository risks,
            AuditService audits, TimeProvider clock, Projects.PhaseGates gates, IEvidenceRepository evidence)
        {
            _gates = gates;
            _evidence = evidence;
            _projects = projects;
            _compliance = compliance;
            _sboms = sboms;
            _risks = risks;
            _audits = audits;
            _clock = clock;
        }

        // Null for another organisation's project. "frameworks" are slugs; none means all the project follows.
        public async Task<PackageReadiness?> CheckAsync(Guid projectId, IReadOnlyCollection<string>? frameworks, CancellationToken cancellationToken = default)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var compliance = project is null ? null : await _compliance.ForProjectAsync(project.Id, cancellationToken);
            if (project is null || compliance is null)
            {
                return null;
            }

            var chosen = compliance.Frameworks
                .Where(f => frameworks is null || frameworks.Count == 0 || frameworks.Contains(FrameworkSlugs.Slug(f.Framework.Name)))
                .ToList();
            var audits = await _audits.ForProjectAsync(project.Id, cancellationToken);
            var facts = new ReadinessFacts(
                (await _sboms.ListAsync(project.Id, cancellationToken)).Count > 0,
                project.SupportEndsOn,
                project.VulnerabilityContactEmail,
                (await _risks.ListAsync(project.Id, cancellationToken)).Count(r => r.Status != RiskStatus.Closed),
                audits?.All.Select(a => (DateOnly?)a.HeldOn).Max());
            var check = AuditReadiness.Check(chosen, facts, DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime));
            var withoutDate = (await _evidence.ListActiveAsync(project.Id, cancellationToken)).Count(e => e.ValidUntil is null && e.OrganisationDocumentId is null);
            check = check with { Frameworks = [.. check.Frameworks, AuditReadiness.Dates(withoutDate)] }; // US-195 R5

            // US-194 R5: the current phase's checklist items that are missing
            return await _gates.LifecycleAsync(project.Id, compliance.Frameworks, cancellationToken) is { } lifecycle
                ? check with { Frameworks = [.. check.Frameworks, AuditReadiness.Lifecycle(lifecycle)] }
                : check;
        }
    }
}
