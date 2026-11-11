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
        private readonly IEvidenceStoryRepository _stories; // US-146
        private readonly Risks.IRiskRepository _risks; // US-146
        private readonly ComplianceQueries _compliance;
        private readonly CurrentMember _member;
        private readonly TimeProvider _clock;

        public EvidenceQueries(
            IProjectRepository projects, IEvidenceRepository evidence, IEvidenceLinkSuggestionRepository suggestions,
            IEvidenceStoryRepository stories, Risks.IRiskRepository risks,
            ComplianceQueries compliance, CurrentMember member, TimeProvider clock)
        {
            _stories = stories;
            _risks = risks;
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

            // US-146: the stories evidence is linked to, the AI's pending story suggestions, and the open risks
            var stories = compliance.WorkItems.Where(w => w.State != Domain.WorkItems.WorkItemState.Removed).ToDictionary(w => w.Id);
            var storyLinks = (await _stories.ListActiveLinksAsync(project.Id, cancellationToken)).Where(l => stories.ContainsKey(l.WorkItemId)).ToList();
            var storySuggestions = (await _stories.ListSuggestionsAsync(project.Id, cancellationToken))
                .Where(s => s.IsPending && stories.ContainsKey(s.WorkItemId)).ToList();
            var risks = (await _risks.ListAsync(project.Id, cancellationToken)).Where(r => r.Status != Domain.Risks.RiskStatus.Closed).ToList();

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
                        .ToList())
                {
                    Stories = storyLinks.Where(l => l.EvidenceId == e.Id).Select(l => new LinkedStory(l, stories[l.WorkItemId])).ToList(),
                    StorySuggestions = storySuggestions.Where(s => s.EvidenceId == e.Id).OrderByDescending(s => s.Score)
                        .Select(s => new SuggestedStory(s, stories[s.WorkItemId])).ToList()
                })
                .Select(row => row with { Trace = EvidenceTraces.For(row, compliance, risks) })
                .ToList();

            // "+ Link requirement" offers the requirements that apply (R5)
            var linkable = compliance.Frameworks
                .SelectMany(f => f.Requirements.Where(r => r.IsApplicable).Select(r => new LinkableRequirement(f.Framework.Name, r.Requirement)))
                .ToList();
            return new EvidenceTab(rows, linkable);
        }
    }

    public record EvidenceTab(IReadOnlyList<EvidenceRow> Evidence, IReadOnlyList<LinkableRequirement> Linkable);

    public record EvidenceRow(Evidence Evidence, bool IsExpired, IReadOnlyList<LinkedRequirement> Links, IReadOnlyList<SuggestedRequirement> Suggestions)
    {
        // US-146 R5, R6: the stories the evidence shows are done, and the AI's pending story suggestions
        public IReadOnlyList<LinkedStory> Stories { get; init; } = [];
        public IReadOnlyList<SuggestedStory> StorySuggestions { get; init; } = [];

        // US-146 R2: the thread the evidence is part of
        public EvidenceTrace Trace { get; init; } = EvidenceTrace.Empty;
    }

    public record LinkedStory(EvidenceStoryLink Link, Domain.WorkItems.WorkItem Story);

    public record SuggestedStory(EvidenceStorySuggestion Suggestion, Domain.WorkItems.WorkItem Story);

    // IsApplicable: R5, a link to a requirement marked not applicable is kept but does not count
    public record LinkedRequirement(EvidenceLink Link, Requirement Requirement, bool IsApplicable);

    public record SuggestedRequirement(EvidenceLinkSuggestion Suggestion, Requirement Requirement);

    public record LinkableRequirement(string Framework, Requirement Requirement);
}
