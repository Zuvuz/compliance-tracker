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
            var target = framework.Versions.Single(v => v.Id == toVersionId).Requirements.ToDictionary(r => r.Code);
            var from = framework.Versions.Single(v => v.Id == fromVersionId).Requirements
                .Where(r => target.ContainsKey(r.Code))
                .ToList();
            var sameCode = from.ToDictionary(r => r.Id, r => target[r.Code].Id);

            // US-110 R2: only requirements whose title or text changed need a person to look again
            var changed = from.Where(r => !r.HasSameTextAs(target[r.Code])).Select(r => r.Id).ToHashSet();
            var toReview = 0;

            var links = await _links.ListActiveAsync(project.Id, cancellationToken);
            var linksCarried = 0;
            foreach (var link in links.Where(l => sameCode.ContainsKey(l.RequirementId)))
            {
                var to = sameCode[link.RequirementId];
                if (!links.Any(l => l.RequirementId == to && l.WorkItemId == link.WorkItemId))
                {
                    var carried = link.CarryTo(to, changed.Contains(link.RequirementId));
                    await _links.AddAsync(carried, cancellationToken);
                    linksCarried++;
                    toReview += carried.NeedsReview ? 1 : 0;
                }
            }

            var decisions = await _applicability.ListActiveAsync(project.Id, cancellationToken);
            var decisionsCarried = 0;
            foreach (var decision in decisions.Where(d => sameCode.ContainsKey(d.RequirementId)))
            {
                var to = sameCode[decision.RequirementId];
                if (!decisions.Any(d => d.RequirementId == to))
                {
                    var carried = decision.CarryTo(to, changed.Contains(decision.RequirementId));
                    await _applicability.AddAsync(carried, cancellationToken);
                    decisionsCarried++;
                    toReview += carried.NeedsReview ? 1 : 0;
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
                    var carried = link.CarryTo(to, changed.Contains(link.RequirementId));
                    await _evidence.AddLinkAsync(carried, cancellationToken);
                    evidenceCarried++;
                    toReview += carried.NeedsReview ? 1 : 0;
                }
            }

            return new CarriedOver(linksCarried, decisionsCarried, evidenceCarried, toReview);
        }
    }

    // How much followed the project to the new version, for the activity log
    // US-110: ToReview counts what was carried to a requirement whose text changed
    public record CarriedOver(int Links, int NotApplicable, int Evidence, int ToReview = 0)
    {
        public string Describe(string framework, int version)
            => $"{framework}: version {version}; carried over {Links} links, {NotApplicable} not applicable, {Evidence} evidence"
               + (ToReview > 0 ? $"; {ToReview} to review because the requirement's text changed" : "");
    }
}
