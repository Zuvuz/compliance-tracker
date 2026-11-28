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
            => await _compliance.ForProjectAsync(projectId, cancellationToken) is { } compliance
                ? await ForProjectAsync(projectId, compliance, cancellationToken)
                : null;

        // US-232: with the project's requirements the caller already has, so a requirement's page reads them once
        public async Task<EvidenceTab?> ForProjectAsync(Guid projectId, ProjectCompliance compliance, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            // The requirements of the versions the project follows now; links to others stay as history
            var requirements = compliance.Frameworks.SelectMany(f => f.Requirements).ToDictionary(r => r.Requirement.Id);
            var links = await _evidence.ListActiveLinksAsync(project.Id, cancellationToken);
            var all = await _suggestions.ListAsync(project.Id, cancellationToken);
            var pending = all.Where(s => s.IsPending).ToList();
            var rejected = all.Where(s => s.Status == SuggestionStatus.Rejected).ToList(); // US-145 R6
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
                        .Select(s => new SuggestedStory(s, stories[s.WorkItemId])).ToList(),
                    Rejected = rejected.Where(s => s.EvidenceId == e.Id && requirements.ContainsKey(s.RequirementId))
                        .OrderByDescending(s => s.DecidedAt).Select(s => new SuggestedRequirement(s, requirements[s.RequirementId].Requirement)).ToList()
                })
                .Select(row => row with
                {
                    Trace = EvidenceTraces.For(row, compliance, risks),
                    Through = EvidenceThrough.For(row.Stories.Select(s => s.Story).ToList(), compliance.Frameworks, row.Links.Select(l => l.Requirement.Id).ToHashSet())
                })
                .ToList();

            // "+ Link requirement" offers the requirements that apply (R5)
            var linkable = compliance.Frameworks
                .SelectMany(f => f.Requirements.Where(r => r.IsApplicable).Select(r => new LinkableRequirement(f.Framework.Name, r.Requirement)))
                .ToList();
            return new EvidenceTab(rows, linkable);
        }
    }

    public record EvidenceTab(IReadOnlyList<EvidenceRow> Evidence, IReadOnlyList<LinkableRequirement> Linkable)
    {
        // US-201 R1, R3: the evidence by type, in the fixed order; a group with none is left out
        public IReadOnlyList<EvidenceGroupView> Groups => EvidenceGroupView.Of(Evidence);
    }

    // US-201 R3, R4: a group's evidence, and how many wait for a decision or have expired (then it starts open)
    public record EvidenceGroupView(EvidenceGroup Group, IReadOnlyList<EvidenceRow> Rows)
    {
        public string Name => EvidenceGroups.NameOf(Group);
        public string Slug => EvidenceGroups.SlugOf(Group);
        public int Waiting => Rows.Count(r => r.Suggestions.Count > 0 || r.StorySuggestions.Count > 0);
        public int Expired => Rows.Count(r => r.IsExpired);
        public bool NeedsAttention => Waiting > 0 || Expired > 0;

        public static IReadOnlyList<EvidenceGroupView> Of(IEnumerable<EvidenceRow> rows)
            => rows.GroupBy(r => r.Evidence.Group)
                .OrderBy(g => g.Key)
                .Select(g => new EvidenceGroupView(g.Key, g.ToList()))
                .ToList();
    }

    public record EvidenceRow(Evidence Evidence, bool IsExpired, IReadOnlyList<LinkedRequirement> Links, IReadOnlyList<SuggestedRequirement> Suggestions)
    {
        // US-146 R5, R6: the stories the evidence shows are done, and the AI's pending story suggestions
        public IReadOnlyList<LinkedStory> Stories { get; init; } = [];
        public IReadOnlyList<SuggestedStory> StorySuggestions { get; init; } = [];

        // US-145 R6: rejected requirement suggestions, so a person can take one back
        public IReadOnlyList<SuggestedRequirement> Rejected { get; init; } = [];

        // US-146 R2: the thread the evidence is part of
        public EvidenceTrace Trace { get; init; } = EvidenceTrace.Empty;

        // US-205 R11: the requirements the evidence reaches through its linked stories, not linked to it directly
        public IReadOnlyList<ThroughStory> Through { get; init; } = [];
    }

    // US-205 R11: "Through [US-012] Lock account: CRA I.2(d)"
    public record ThroughStory(Domain.WorkItems.WorkItem Story, Requirement Requirement, string Label);

    public static class EvidenceThrough
    {
        // Known without asking the AI: each linked story's requirements (applicable, not waiting for review), except those the
        // evidence is linked to directly; each requirement once, under the first story that reaches it
        public static IReadOnlyList<ThroughStory> For(IReadOnlyList<Domain.WorkItems.WorkItem> stories, IReadOnlyList<FrameworkCompliance> frameworks,
            IReadOnlySet<Guid> direct)
        {
            var through = new List<ThroughStory>();
            foreach (var story in stories.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var framework in frameworks)
                {
                    foreach (var row in framework.Requirements.Where(r => r.IsApplicable && r.Links.Any(l => l.WorkItem.Id == story.Id && !l.Link.NeedsReview)))
                    {
                        if (!direct.Contains(row.Requirement.Id) && through.All(t => t.Requirement.Id != row.Requirement.Id))
                        {
                            through.Add(new ThroughStory(story, row.Requirement, $"{Frameworks.FrameworkSlugs.Short(framework.Framework.Name)} {row.Requirement.Code}"));
                        }
                    }
                }
            }

            return through;
        }
    }

    public record LinkedStory(EvidenceStoryLink Link, Domain.WorkItems.WorkItem Story);

    public record SuggestedStory(EvidenceStorySuggestion Suggestion, Domain.WorkItems.WorkItem Story);

    // IsApplicable: R5, a link to a requirement marked not applicable is kept but does not count
    public record LinkedRequirement(EvidenceLink Link, Requirement Requirement, bool IsApplicable);

    public record SuggestedRequirement(EvidenceLinkSuggestion Suggestion, Requirement Requirement);

    public record LinkableRequirement(string Framework, Requirement Requirement);
}
