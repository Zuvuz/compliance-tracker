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

        public PhaseGates(IProjectRepository projects, IFrameworkRepository frameworks, IClassificationRepository classifications,
            IRiskRepository risks, CurrentMember member, ComplianceQueries compliance, IScanRepository scans, ISbomRepository sboms)
        {
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
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project
                || project.IsClosed)
            {
                return null;
            }

            return project.Phase switch
            {
                LifecyclePhase.Planning => await ForAsync(organization, project, LifecyclePhase.Development, cancellationToken),
                LifecyclePhase.Development => await ForAsync(organization, project, LifecyclePhase.Released, cancellationToken),
                _ => null
            };
        }

        // The gate between the project's phase and "to"; null when that move has none (e.g. back to an earlier phase).
        // US-072: "supportEndsOn" is a support end date sent with the release itself (US-025 R1).
        public async Task<PhaseGate?> ForAsync(Organization organization, Project project, LifecyclePhase to, CancellationToken cancellationToken = default,
            DateOnly? supportEndsOn = null)
            => (project.Phase, to) switch
            {
                (LifecyclePhase.Planning, LifecyclePhase.Development) => new PhaseGate(ReadyForDevelopment, project.Phase, to,
                    await ForDevelopmentAsync(organization, project, cancellationToken)),
                (LifecyclePhase.Planning or LifecyclePhase.Development, LifecyclePhase.Released) => new PhaseGate(ReadyForRelease, project.Phase, to,
                    await ForReleaseAsync(project, supportEndsOn, cancellationToken)),
                _ => null
            };

        // US-072 R1: coverage, evidence, no open critical or high findings, an SBOM and a support period
        private async Task<IReadOnlyList<GateCheck>> ForReleaseAsync(Project project, DateOnly? supportEndsOn, CancellationToken cancellationToken)
        {
            var frameworks = (await _compliance.ForProjectAsync(project.Id, cancellationToken))?.Frameworks ?? [];
            var findings = await _scans.ListFindingsAsync(project.Id, cancellationToken);
            var scanned = (await _scans.ListAsync(project.Id, cancellationToken)).Count > 0;
            var latestSbom = SbomService.Latest(await _sboms.ListAsync(project.Id, cancellationToken)).FirstOrDefault();
            return ReleaseChecks(new ReleaseFacts(
                frameworks.Count,
                frameworks.Sum(f => f.NotCovered),
                frameworks.Sum(f => f.Requirements.Count(r => r.IsApplicable && !r.Requirement.IsRecommendation && r.IsCovered && !r.HasEvidence)),
                scanned,
                findings.Count(f => f.IsOpen && f.Severity == Domain.Scanning.FindingSeverity.Critical),
                findings.Count(f => f.IsOpen && f.Severity == Domain.Scanning.FindingSeverity.High),
                latestSbom?.ProductVersion,
                project.SupportEndsOn ?? supportEndsOn));
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

        // US-071 R1: purpose and scope, a framework, a class under each regulation it follows, and a first risk
        private async Task<IReadOnlyList<GateCheck>> ForDevelopmentAsync(Organization organization, Project project, CancellationToken cancellationToken)
        {
            var classified = (await _classifications.ListAsync(project.Id, cancellationToken)).Select(c => c.FrameworkId).ToHashSet();
            var unclassified = new List<string>();
            foreach (var used in project.Frameworks)
            {
                if (await _frameworks.GetAvailableAsync(organization.Id, used.FrameworkId, cancellationToken) is { } framework
                    && RegulationClasses.For(framework) is not null
                    && !classified.Contains(framework.Id))
                {
                    unclassified.Add(FrameworkSlugs.Short(framework.Name));
                }
            }

            return DevelopmentChecks(project, unclassified, (await _risks.ListAsync(project.Id, cancellationToken)).Count);
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
