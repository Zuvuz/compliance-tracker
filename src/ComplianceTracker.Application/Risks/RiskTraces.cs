using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Risks
{
    // US-124: the thread from a risk through its measures and actions to user stories, requirements and evidence, and
    // back from a requirement to the open risks that reach it. Built from data that is already there (US-042, US-111,
    // US-021, US-059); nothing new is stored.

    // A requirement with the regulation's short name and the evidence on it
    public record TraceRequirement(Requirement Requirement, string Framework, IReadOnlyList<EvidenceItem> Evidence);

    // A user story with the requirements it covers
    public record TraceStory(WorkItem Story, IReadOnlyList<TraceRequirement> Requirements);

    // An action with its stories (and their requirements) and the evidence linked to the action itself
    public record TraceAction(RiskAction Action, IReadOnlyList<TraceStory> Stories, IReadOnlyList<Guid> EvidenceIds);

    // R1: the whole thread of one risk
    public record RiskTrace(IReadOnlyList<TraceRequirement> MeasureRequirements, IReadOnlyList<TraceStory> MeasureStories, IReadOnlyList<TraceAction> Actions)
    {
        public bool IsEmpty => MeasureRequirements.Count == 0 && MeasureStories.Count == 0 && Actions.Count == 0;
    }

    // R2: a risk as a requirement row shows it
    public record RiskReference(Guid Id, string Title, RiskLevel Level);

    public static class RiskTraces
    {
        // R1, R4: removed stories and requirements the project no longer follows are left out
        public static RiskTrace For(Risk risk, ProjectCompliance compliance)
        {
            var rows = compliance.Frameworks
                .SelectMany(f => f.Requirements.Select(r => (Framework: FrameworkSlugs.Short(f.Framework.Name), Row: r)))
                .ToList();
            var requirements = rows.ToDictionary(r => r.Row.Requirement.Id);
            var stories = compliance.WorkItems.Where(w => w.State != WorkItemState.Removed).ToDictionary(w => w.Id);

            TraceRequirement Requirement((string Framework, RequirementCoverage Row) item)
                => new(item.Row.Requirement, item.Framework, item.Row.Evidence);

            TraceStory Story(WorkItem story)
                => new(story, rows.Where(r => r.Row.Links.Any(l => l.WorkItem.Id == story.Id)).Select(Requirement).ToList());

            return new RiskTrace(
                risk.Measures.Where(m => m.RequirementId is { } id && requirements.ContainsKey(id))
                    .Select(m => Requirement(requirements[m.RequirementId!.Value])).ToList(),
                risk.Measures.Where(m => m.WorkItemId is { } id && stories.ContainsKey(id))
                    .Select(m => Story(stories[m.WorkItemId!.Value])).ToList(),
                risk.Actions.Select(a => new TraceAction(
                        a,
                        a.Links.Where(l => l.WorkItemId is { } id && stories.ContainsKey(id)).Select(l => Story(stories[l.WorkItemId!.Value])).ToList(),
                        a.Links.Where(l => l.EvidenceId is not null).Select(l => l.EvidenceId!.Value).ToList()))
                    .ToList());
        }

        // R2, R3: per requirement, the open risks that reach it as a measure, or through a story that is a measure or is
        // linked to one of the risk's actions. requirementsOfStory: the requirements each story covers (active links).
        // Highest risk first.
        public static IReadOnlyDictionary<Guid, IReadOnlyList<RiskReference>> ByRequirement(
            IEnumerable<Risk> risks, ILookup<Guid, Guid> requirementsOfStory)
        {
            var reached = new Dictionary<Guid, List<Risk>>();
            foreach (var risk in risks.Where(r => r.Status != RiskStatus.Closed))
            {
                var storyIds = risk.Measures.Select(m => m.WorkItemId)
                    .Concat(risk.Actions.SelectMany(a => a.Links).Select(l => l.WorkItemId))
                    .OfType<Guid>();
                var requirementIds = risk.Measures.Select(m => m.RequirementId).OfType<Guid>()
                    .Concat(storyIds.SelectMany(id => requirementsOfStory[id]))
                    .Distinct();
                foreach (var requirementId in requirementIds)
                {
                    if (!reached.TryGetValue(requirementId, out var list))
                    {
                        list = [];
                        reached[requirementId] = list;
                    }

                    list.Add(risk);
                }
            }

            return reached.ToDictionary(
                r => r.Key,
                r => (IReadOnlyList<RiskReference>)r.Value.OrderByDescending(x => x.Score).ThenBy(x => x.Title, StringComparer.Ordinal)
                    .Select(x => new RiskReference(x.Id, x.Title, x.Level)).ToList());
        }

        // e.g. "Stolen sessions (High); Phishing (Medium)"
        public static string Text(IEnumerable<RiskReference> risks) => string.Join("; ", risks.Select(r => $"{r.Title} ({r.Level})"));
    }
}
