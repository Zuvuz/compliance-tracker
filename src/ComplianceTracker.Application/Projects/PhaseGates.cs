using ComplianceTracker.Application.Common;
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

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IClassificationRepository _classifications;
        private readonly IRiskRepository _risks;
        private readonly CurrentMember _member;

        public PhaseGates(IProjectRepository projects, IFrameworkRepository frameworks, IClassificationRepository classifications,
            IRiskRepository risks, CurrentMember member)
        {
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
                _ => null
            };
        }

        // The gate between the project's phase and "to"; null when that move has none (e.g. back to an earlier phase)
        public async Task<PhaseGate?> ForAsync(Organization organization, Project project, LifecyclePhase to, CancellationToken cancellationToken = default)
            => (project.Phase, to) switch
            {
                (LifecyclePhase.Planning, LifecyclePhase.Development) => new PhaseGate(ReadyForDevelopment, project.Phase, to,
                    await ForDevelopmentAsync(organization, project, cancellationToken)),
                _ => null
            };

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
