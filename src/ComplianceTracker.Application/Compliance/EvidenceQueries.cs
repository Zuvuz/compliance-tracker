using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Compliance
{
    // US-059: what the Evidence tab shows. R9: everyone in the organisation, also the Auditor; null for other
    // organisations' projects.
    public class EvidenceQueries
    {
        private readonly IProjectRepository _projects;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceLinkSuggestionRepository _suggestions;
        private readonly ComplianceQueries _compliance;
        private readonly CurrentMember _member;
        private readonly TimeProvider _clock;

        public EvidenceQueries(
            IProjectRepository projects, IEvidenceRepository evidence, IEvidenceLinkSuggestionRepository suggestions,
            ComplianceQueries compliance, CurrentMember member, TimeProvider clock)
        {
            _projects = projects;
            _evidence = evidence;
            _suggestions = suggestions;
            _compliance = compliance;
            _member = member;
            _clock = clock;
        }

        public async Task<EvidenceTab?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project
                || await _compliance.ForProjectAsync(projectId, cancellationToken) is not { } compliance)
            {
                return null;
            }

            // The requirements of the versions the project follows now; links to others stay as history
            var requirements = compliance.Frameworks.SelectMany(f => f.Requirements).ToDictionary(r => r.Requirement.Id);
            var links = await _evidence.ListActiveLinksAsync(project.Id, cancellationToken);
            var pending = (await _suggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.IsPending).ToList();
            var today = _clock.Today();

            var rows = (await _evidence.ListActiveAsync(project.Id, cancellationToken))
                .OrderByDescending(e => e.AddedAt)
                .Select(e => new EvidenceRow(
                    e,
                    e.IsExpiredOn(today),
                    links.Where(l => l.EvidenceId == e.Id && requirements.ContainsKey(l.RequirementId))
                        .Select(l => new LinkedRequirement(l, requirements[l.RequirementId].Requirement, requirements[l.RequirementId].IsApplicable))
                        .OrderBy(l => l.Requirement.Position)
                        .ToList(),
                    pending.Where(s => s.EvidenceId == e.Id && requirements.ContainsKey(s.RequirementId))
                        .OrderByDescending(s => s.Score)
                        .Select(s => new SuggestedRequirement(s, requirements[s.RequirementId].Requirement))
                        .ToList()))
                .ToList();

            // "+ Link requirement" offers the requirements that apply (R5)
            var linkable = compliance.Frameworks
                .SelectMany(f => f.Requirements.Where(r => r.IsApplicable).Select(r => new LinkableRequirement(f.Framework.Name, r.Requirement)))
                .ToList();
            return new EvidenceTab(rows, linkable);
        }
    }

    public record EvidenceTab(IReadOnlyList<EvidenceRow> Evidence, IReadOnlyList<LinkableRequirement> Linkable);

    public record EvidenceRow(Evidence Evidence, bool IsExpired, IReadOnlyList<LinkedRequirement> Links, IReadOnlyList<SuggestedRequirement> Suggestions);

    // IsApplicable: R5, a link to a requirement marked not applicable is kept but does not count
    public record LinkedRequirement(EvidenceLink Link, Requirement Requirement, bool IsApplicable);

    public record SuggestedRequirement(EvidenceLinkSuggestion Suggestion, Requirement Requirement);

    public record LinkableRequirement(string Framework, Requirement Requirement);
}
