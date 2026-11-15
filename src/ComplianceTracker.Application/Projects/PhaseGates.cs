using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Projects
{
    // US-071: works out the gate a project passes when it moves on, from what the tracker already knows
    public class PhaseGates
    {
        public const string ReadyForDevelopment = "Ready for Development";
        public const string ReadyForRelease = "Ready for release";

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IClassificationRepository _classifications;
        private readonly IRiskRepository _risks;
        private readonly CurrentMember _member;
        private readonly ComplianceQueries _compliance;
        private readonly IScanRepository _scans;
        private readonly ISbomRepository _sboms;
        private readonly IProjectChecklistRepository _checklist; // US-047

        public PhaseGates(IProjectRepository projects, IFrameworkRepository frameworks, IClassificationRepository classifications,
            IRiskRepository risks, CurrentMember member, ComplianceQueries compliance, IScanRepository scans, ISbomRepository sboms,
            IProjectChecklistRepository checklist)
        {
            _checklist = checklist;
            _compliance = compliance;
            _scans = scans;
            _sboms = sboms;
            _projects = projects;
            _frameworks = frameworks;
            _classifications = classifications;
            _risks = risks;
            _member = member;
        }

        // The Overview: the gate to the project's next phase, or null when its phase has none. Null for other organisations.
        public async Task<PhaseGate?> NextAsync(Guid projectId, CancellationToken cancellationToken = default)
            => (await OverviewAsync(projectId, cancellationToken)).Gate;

        // US-047: the gate to the next phase and the current phase's checklist, from one look-up of the facts
        public async Task<(PhaseGate? Gate, PhaseChecklistView? Checklist)> OverviewAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project
                || project.IsOrganisationArea)
            {
                return (null, null);
            }

            // The gate is shown for the move it has always been shown for: Planning and Design to Development, Development and
            // Verification to release
            LifecyclePhase? next = project.IsClosed ? null : project.Phase switch
            {
                LifecyclePhase.Planning or LifecyclePhase.Design => LifecyclePhase.Development,
                LifecyclePhase.Development or LifecyclePhase.Verification => LifecyclePhase.Released,
                _ => null
            };

            // Only what the current phase's items and the gate need is looked up (US-139: the Overview stays within its queries)
            var needs = BuiltInChecklist.All.Where(i => i.Phase >= project.Phase && i.Phase < (next ?? project.Phase + 1)).Select(i => i.Check).ToHashSet();
            if (next == LifecyclePhase.Development)
            {
                needs.UnionWith([ChecklistCheck.Scope, ChecklistCheck.Classification, ChecklistCheck.Risks]);
            }
            else if (next == LifecyclePhase.Released)
            {
                needs.UnionWith([ChecklistCheck.Coverage, ChecklistCheck.Evidence, ChecklistCheck.Findings, ChecklistCheck.Sbom, ChecklistCheck.Support]);
            }

            var facts = await FactsAsync(organization, project, null, cancellationToken, needs);
            var lines = ChecklistRules.Lines(BuiltInChecklist.For(facts.Frameworks), await _checklist.ListAsync(project.Id, cancellationToken), facts.Checklist);
            var current = lines.Where(l => l.Item.Phase == project.Phase).ToList();
            var checklist = new PhaseChecklistView(project.Phase,
                current.Where(l => l.State != ChecklistState.NotChosen).ToList(),
                current.Where(l => l.State == ChecklistState.NotChosen).ToList());
            return (next is { } to ? Gate(project, to, facts, lines) : null, checklist);
        }

        // The gate between the project's phase and "to"; null when that move has none (e.g. back to an earlier phase).
        // US-072: "supportEndsOn" is a support end date sent with the release itself (US-025 R1).
        // US-047 R5: the checklist items still missing in the phases the move leaves behind are listed with the gate; they warn
        // and are logged, but need no reason.
        public async Task<PhaseGate?> ForAsync(Organization organization, Project project, LifecyclePhase to, CancellationToken cancellationToken = default,
            DateOnly? supportEndsOn = null)
        {
            var facts = await FactsAsync(organization, project, supportEndsOn, cancellationToken);
            var lines = ChecklistRules.Lines(BuiltInChecklist.For(facts.Frameworks), await _checklist.ListAsync(project.Id, cancellationToken), facts.Checklist);
            return Gate(project, to, facts, lines);
        }

        private static PhaseGate? Gate(Project project, LifecyclePhase to, AllFacts facts, IReadOnlyList<ChecklistLine> lines)
        {
            PhaseGate? gate = null;
            if (project.Phase == LifecyclePhase.Planning && to is LifecyclePhase.Design or LifecyclePhase.Development)
            {
                gate = new PhaseGate(ReadyForDevelopment, project.Phase, to, DevelopmentChecks(project, facts.Unclassified, facts.Checklist.Risks));
            }
            else if (project.Phase < LifecyclePhase.Released && to == LifecyclePhase.Released)
            {
                gate = new PhaseGate(ReadyForRelease, project.Phase, to, ReleaseChecks(facts.Release));
            }

            // What the old gate checks already lists is not listed twice
            var missing = ChecklistRules.MissingBefore(lines, project.Phase, to)
                .Where(l => gate is null || l.Item.Check is ChecklistCheck.Manual or ChecklistCheck.Retained)
                .Select(l => $"{l.Item.Text} ({l.Item.Source})")
                .ToList();
            if (gate is null && missing.Count == 0)
            {
                return null;
            }

            return (gate ?? new PhaseGate($"Ready for {to}", project.Phase, to, [])) with { ChecklistMissing = missing };
        }

        private sealed record AllFacts(ChecklistFacts Checklist, ReleaseFacts Release, IReadOnlyList<string> Unclassified, IReadOnlyList<string> Frameworks);

        // Everything the gates and the checklist are worked out from, looked up once
        // "needs": what to look up; null for everything (a phase change)
        private async Task<AllFacts> FactsAsync(Organization organization, Project project, DateOnly? supportEndsOn, CancellationToken cancellationToken,
            IReadOnlySet<ChecklistCheck>? needs = null)
        {
            bool Needs(params ChecklistCheck[] checks) => needs is null || checks.Any(needs.Contains);
            var classified = Needs(ChecklistCheck.Classification)
                ? (await _classifications.ListAsync(project.Id, cancellationToken)).Select(c => c.FrameworkId).ToHashSet()
                : null;
            var unclassified = new List<string>();
            var names = new List<string>();
            foreach (var used in project.Frameworks)
            {
                if (await _frameworks.GetAvailableAsync(organization.Id, used.FrameworkId, cancellationToken) is { } framework)
                {
                    names.Add(FrameworkSlugs.Short(framework.Name));
                    if (classified is not null && RegulationClasses.For(framework) is not null && !classified.Contains(framework.Id))
                    {
                        unclassified.Add(FrameworkSlugs.Short(framework.Name));
                    }
                }
            }

            var risks = Needs(ChecklistCheck.Risks) ? (await _risks.ListAsync(project.Id, cancellationToken)).Count : 0;
            var frameworks = Needs(ChecklistCheck.Coverage, ChecklistCheck.Evidence)
                ? (await _compliance.ForProjectAsync(project.Id, cancellationToken))?.Frameworks ?? []
                : [];
            var findings = Needs(ChecklistCheck.Findings) ? await _scans.ListFindingsAsync(project.Id, cancellationToken) : [];
            var scanned = Needs(ChecklistCheck.Findings) && (await _scans.ListAsync(project.Id, cancellationToken)).Count > 0;
            var latestSbom = Needs(ChecklistCheck.Sbom) ? SbomService.Latest(await _sboms.ListAsync(project.Id, cancellationToken)).FirstOrDefault() : null;
            var release = new ReleaseFacts(
                Needs(ChecklistCheck.Coverage, ChecklistCheck.Evidence) ? frameworks.Count : project.Frameworks.Count(),
                frameworks.Sum(f => f.NotCovered),
                frameworks.Sum(f => f.Requirements.Count(r => r.IsApplicable && !r.Requirement.IsRecommendation && r.IsCovered && !r.HasEvidence)),
                scanned,
                findings.Count(f => f.IsOpen && f.Severity == Domain.Scanning.FindingSeverity.Critical),
                findings.Count(f => f.IsOpen && f.Severity == Domain.Scanning.FindingSeverity.High),
                latestSbom?.ProductVersion,
                project.SupportEndsOn ?? supportEndsOn);
            var checklist = new ChecklistFacts(
                !string.IsNullOrWhiteSpace(project.IntendedPurpose) && !string.IsNullOrWhiteSpace(project.Scope),
                unclassified.ToHashSet(),
                risks,
                release.Frameworks,
                release.NotCovered,
                release.WithoutEvidence,
                release.Scanned,
                release.OpenCritical + release.OpenHigh,
                release.SbomVersion is not null,
                release.SupportEndsOn is not null,
                project.RetainUntil is not null);
            return new AllFacts(checklist, release, unclassified, names);
        }

        // What the release checks are worked out from
        public record ReleaseFacts(int Frameworks, int NotCovered, int WithoutEvidence, bool Scanned, int OpenCritical, int OpenHigh,
            string? SbomVersion, DateOnly? SupportEndsOn);

        // US-072 R1, R2: each check with the number missing; pure, so it can be tested without a database
        public static IReadOnlyList<GateCheck> ReleaseChecks(ReleaseFacts facts)
        {
            var findings = new List<string>();
            if (facts.OpenCritical > 0)
            {
                findings.Add($"{facts.OpenCritical} open critical {(facts.OpenCritical == 1 ? "finding" : "findings")}");
            }

            if (facts.OpenHigh > 0)
            {
                findings.Add($"{facts.OpenHigh} open high {(facts.OpenHigh == 1 ? "finding" : "findings")}");
            }

            return
            [
                facts.Frameworks == 0
                    ? new GateCheck("coverage", "Follows no framework", GateCheckState.NotMet)
                    : facts.NotCovered > 0
                        ? new GateCheck("coverage", $"{Plural(facts.NotCovered, "requirement", "requirements")} not covered", GateCheckState.NotMet)
                        : new GateCheck("coverage", "Every applicable requirement is covered", GateCheckState.Met),
                facts.WithoutEvidence > 0
                    ? new GateCheck("evidence", $"{Plural(facts.WithoutEvidence, "requirement", "requirements")} without evidence", GateCheckState.NotMet)
                    : new GateCheck("evidence", "Every covered requirement has evidence", GateCheckState.Met),
                !facts.Scanned
                    ? new GateCheck("findings", "No security scan yet", GateCheckState.NotMet)
                    : findings.Count > 0
                        ? new GateCheck("findings", string.Join(" and ", findings), GateCheckState.NotMet)
                        : new GateCheck("findings", "No open critical or high findings", GateCheckState.Met),
                facts.SbomVersion is { } version
                    ? new GateCheck("sbom", $"SBOM uploaded (version {version})", GateCheckState.Met)
                    : new GateCheck("sbom", "No SBOM uploaded", GateCheckState.NotMet),
                facts.SupportEndsOn is { } ends
                    ? new GateCheck("support", $"Supported until {ends:dd-MM-yyyy}", GateCheckState.Met)
                    : new GateCheck("support", "No support end date", GateCheckState.NotMet)
            ];
        }

        // The checks themselves, from what was looked up; pure, so they can be tested without a database
        public static IReadOnlyList<GateCheck> DevelopmentChecks(Project project, IReadOnlyList<string> unclassified, int risks)
        {
            var frameworks = project.Frameworks.Count();
            return
            [
                string.IsNullOrWhiteSpace(project.IntendedPurpose) || string.IsNullOrWhiteSpace(project.Scope)
                    ? new GateCheck("scope", "No intended purpose and scope described", GateCheckState.NotMet)
                    : new GateCheck("scope", "Intended purpose and scope are described", GateCheckState.Met),
                frameworks == 0
                    ? new GateCheck("frameworks", "Follows no framework", GateCheckState.NotMet)
                    : new GateCheck("frameworks", $"Follows {Plural(frameworks, "framework", "frameworks")}", GateCheckState.Met),
                unclassified.Count == 0
                    ? new GateCheck("classification", "Classified under each regulation it follows", GateCheckState.Met)
                    : new GateCheck("classification", $"Not classified under {string.Join(", ", unclassified)}", GateCheckState.NotMet),
                risks == 0
                    ? new GateCheck("risks", "No risks in the register", GateCheckState.NotMet)
                    : new GateCheck("risks", $"{Plural(risks, "risk", "risks")} in the register", GateCheckState.Met)
            ];
        }

        private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
    }
}
