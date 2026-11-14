using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Risks
{
    // US-159 R1, R2: what stands behind one risk in the register: its measures, the user stories behind them and the
    // evidence, each counted once
    public record RiskBacking(int Measures, int Stories, int Evidence, bool NothingBehind)
    {
        public static readonly RiskBacking None = new(0, 0, 0, false);

        // e.g. "2 measures · 1 story · 3 evidence"
        public string Text => $"{Measures} {(Measures == 1 ? "measure" : "measures")} · {Stories} {(Stories == 1 ? "story" : "stories")} · {Evidence} evidence";
    }

    // The links of a project the backing is counted from. Stories: the stories that are not removed.
    public record ProjectLinks(
        ILookup<Guid, Guid> StoriesOfRequirement,
        ILookup<Guid, Guid> EvidenceOfRequirement,
        ILookup<Guid, Guid> EvidenceOfStory,
        IReadOnlySet<Guid> Stories)
    {
        // From the Compliance data that the Overview already has (no extra queries)
        public static ProjectLinks From(ProjectCompliance compliance)
        {
            var rows = compliance.Frameworks.SelectMany(f => f.Requirements).ToList();
            return new ProjectLinks(
                rows.SelectMany(r => r.Links.Select(l => (r.Requirement.Id, l.WorkItem.Id))).ToLookup(x => x.Item1, x => x.Item2),
                rows.SelectMany(r => r.Evidence.Where(e => !e.IsExpired).Select(e => (r.Requirement.Id, e.Evidence.Id))).ToLookup(x => x.Item1, x => x.Item2),
                compliance.StoryEvidence.ToLookup(e => e.WorkItemId, e => e.Evidence.Id),
                compliance.WorkItems.Where(w => w.State != WorkItemState.Removed).Select(w => w.Id).ToHashSet());
        }
    }

    public static class RiskBackings
    {
        // R1–R3. Through a measure that is a requirement: the stories linked to it and its evidence. Through a measure that
        // is a story: the story and the evidence on it. And the stories and evidence linked to the risk's actions.
        public static RiskBacking For(Risk risk, ProjectLinks links)
        {
            var stories = new HashSet<Guid>();
            var evidence = new HashSet<Guid>();
            void Story(Guid id)
            {
                if (links.Stories.Contains(id) && stories.Add(id))
                {
                    evidence.UnionWith(links.EvidenceOfStory[id]);
                }
            }

            foreach (var measure in risk.Measures)
            {
                if (measure.RequirementId is { } requirement)
                {
                    foreach (var story in links.StoriesOfRequirement[requirement])
                    {
                        Story(story);
                    }

                    evidence.UnionWith(links.EvidenceOfRequirement[requirement]);
                }

                if (measure.WorkItemId is { } workItem)
                {
                    Story(workItem);
                }
            }

            foreach (var link in risk.Actions.SelectMany(a => a.Links))
            {
                if (link.WorkItemId is { } story)
                {
                    Story(story);
                }

                if (link.EvidenceId is { } id)
                {
                    evidence.Add(id);
                }
            }

            // R2, R3: only an open, serious risk to reduce can have nothing behind it
            var serious = risk.Status == RiskStatus.Open && risk.Treatment == RiskTreatment.Reduce && risk.Level >= RiskLevel.High;
            var nothing = serious && (risk.Measures.Count == 0 || (stories.Count == 0 && evidence.Count == 0));
            return new RiskBacking(risk.Measures.Count, stories.Count, evidence.Count, nothing);
        }

        public static IReadOnlyDictionary<Guid, RiskBacking> For(IEnumerable<Risk> risks, ProjectLinks links)
            => risks.ToDictionary(r => r.Id, r => For(r, links));
    }

    // R5: the register's backing in a fixed number of queries, whatever the number of risks
    public class RiskBackingQueries
    {
        private readonly IRequirementLinkRepository _links;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceStoryRepository _storyEvidence;
        private readonly IWorkItemRepository _workItems;
        private readonly TimeProvider _clock;

        public RiskBackingQueries(IRequirementLinkRepository links, IEvidenceRepository evidence, IEvidenceStoryRepository storyEvidence,
            IWorkItemRepository workItems, TimeProvider clock)
        {
            _links = links;
            _evidence = evidence;
            _storyEvidence = storyEvidence;
            _workItems = workItems;
            _clock = clock;
        }

        // The project is the caller's own (the Risks tab has checked it)
        public async Task<ProjectLinks> LinksAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            var valid = (await _evidence.ListActiveAsync(projectId, cancellationToken)).Where(e => !e.IsExpiredOn(today)).Select(e => e.Id).ToHashSet();
            var requirementLinks = await _links.ListActiveAsync(projectId, cancellationToken);
            var evidenceLinks = await _evidence.ListActiveLinksAsync(projectId, cancellationToken);
            var storyLinks = await _storyEvidence.ListActiveLinksAsync(projectId, cancellationToken);
            var stories = (await _workItems.ListAsync(projectId, cancellationToken)).Where(w => w.State != WorkItemState.Removed).Select(w => w.Id).ToHashSet();
            return new ProjectLinks(
                requirementLinks.ToLookup(l => l.RequirementId, l => l.WorkItemId),
                evidenceLinks.Where(l => valid.Contains(l.EvidenceId)).ToLookup(l => l.RequirementId, l => l.EvidenceId),
                storyLinks.Where(l => valid.Contains(l.EvidenceId)).ToLookup(l => l.WorkItemId, l => l.EvidenceId),
                stories);
        }
    }
}
