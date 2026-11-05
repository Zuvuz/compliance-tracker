using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Application.Compliance
{
    // US-069 R6 (US-035): when a project moves to another version of a framework, its links, "not applicable" decisions
    // and evidence follow to the requirement with the same code in the new version. A code that is gone in the new
    // version is left behind (it stays on the old version as history). Nothing is copied twice, so moving back and forth
    // is safe. The caller saves.
    public class VersionCarryOver
    {
        private readonly IFrameworkRepository _frameworks;
        private readonly IRequirementLinkRepository _links;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IEvidenceRepository _evidence;

        public VersionCarryOver(
            IFrameworkRepository frameworks,
            IRequirementLinkRepository links,
            IRequirementApplicabilityRepository applicability,
            IEvidenceRepository evidence)
        {
            _frameworks = frameworks;
            _links = links;
            _applicability = applicability;
            _evidence = evidence;
        }

        public async Task<CarriedOver> CarryAsync(
            Project project, Guid frameworkId, Guid fromVersionId, Guid toVersionId, CancellationToken cancellationToken = default)
        {
            var framework = (await _frameworks.ListAvailableWithRequirementsAsync(project.OrganizationId, [frameworkId], cancellationToken)).Single();
            var target = framework.Versions.Single(v => v.Id == toVersionId).Requirements.ToDictionary(r => r.Code, r => r.Id);
            var sameCode = framework.Versions.Single(v => v.Id == fromVersionId).Requirements
                .Where(r => target.ContainsKey(r.Code))
                .ToDictionary(r => r.Id, r => target[r.Code]);

            var links = await _links.ListActiveAsync(project.Id, cancellationToken);
            var linksCarried = 0;
            foreach (var link in links.Where(l => sameCode.ContainsKey(l.RequirementId)))
            {
                var to = sameCode[link.RequirementId];
                if (!links.Any(l => l.RequirementId == to && l.WorkItemId == link.WorkItemId))
                {
                    await _links.AddAsync(link.CarryTo(to), cancellationToken);
                    linksCarried++;
                }
            }

            var decisions = await _applicability.ListActiveAsync(project.Id, cancellationToken);
            var decisionsCarried = 0;
            foreach (var decision in decisions.Where(d => sameCode.ContainsKey(d.RequirementId)))
            {
                var to = sameCode[decision.RequirementId];
                if (!decisions.Any(d => d.RequirementId == to))
                {
                    await _applicability.AddAsync(decision.CarryTo(to), cancellationToken);
                    decisionsCarried++;
                }
            }

            // US-059: the evidence is stored once; only its links move
            var evidenceLinks = await _evidence.ListActiveLinksAsync(project.Id, cancellationToken);
            var evidenceCarried = 0;
            foreach (var link in evidenceLinks.Where(l => sameCode.ContainsKey(l.RequirementId)))
            {
                var to = sameCode[link.RequirementId];
                if (!evidenceLinks.Any(l => l.RequirementId == to && l.EvidenceId == link.EvidenceId))
                {
                    await _evidence.AddLinkAsync(link.CarryTo(to), cancellationToken);
                    evidenceCarried++;
                }
            }

            return new CarriedOver(linksCarried, decisionsCarried, evidenceCarried);
        }
    }

    // How much followed the project to the new version, for the activity log
    public record CarriedOver(int Links, int NotApplicable, int Evidence)
    {
        public string Describe(string framework, int version)
            => $"{framework}: version {version}; carried over {Links} links, {NotApplicable} not applicable, {Evidence} evidence";
    }
}
