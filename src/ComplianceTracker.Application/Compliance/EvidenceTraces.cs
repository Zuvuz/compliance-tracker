using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Compliance
{
    // US-146: a part of an evidence's thread and how it is connected, e.g. ("Late reporting", "via Art. 14(4)(a)")
    public record TraceEntry(Guid Id, string Title, string Via, string? Detail = null);

    // US-146 R2: the thread of one piece of evidence, in a fixed order: the risks it helps reduce, the requirements it
    // proves, the stories it shows are done
    public record EvidenceTrace(IReadOnlyList<TraceEntry> Risks, IReadOnlyList<TraceEntry> Requirements, IReadOnlyList<TraceEntry> Stories)
    {
        public static readonly EvidenceTrace Empty = new([], [], []);

        public bool IsEmpty => Risks.Count == 0 && Requirements.Count == 0 && Stories.Count == 0;
    }

    // US-146: builds the thread from what is stored: the evidence's requirement and story links, the stories linked to
    // those requirements (US-021), and the open risks whose measures or actions are those requirements or stories (US-124),
    // or whose actions name the evidence itself. Nothing new is stored for risks (R1).
    public static class EvidenceTraces
    {
        public static EvidenceTrace For(EvidenceRow row, ProjectCompliance compliance, IReadOnlyList<Risk> openRisks)
        {
            var requirementIds = row.Links.Where(l => l.IsApplicable).Select(l => l.Requirement.Id).ToHashSet();
            var coverage = compliance.Frameworks.SelectMany(f => f.Requirements).Where(r => requirementIds.Contains(r.Requirement.Id)).ToList();

            // R4: stories linked to the evidence, then stories linked to its requirements ("via …"); each once
            var stories = new List<(WorkItem Story, string Via)>();
            foreach (var linked in row.Stories)
            {
                stories.Add((linked.Story, linked.Link.Source == Domain.Mappings.LinkSource.AiApproved ? "AI suggestion approved" : "linked to the evidence"));
            }

            foreach (var requirement in coverage)
            {
                foreach (var link in requirement.Links.Where(l => l.WorkItem.State != WorkItemState.Removed && !l.Link.NeedsReview))
                {
                    if (stories.All(s => s.Story.Id != link.WorkItem.Id))
                    {
                        stories.Add((link.WorkItem, $"via {requirement.Requirement.Code}"));
                    }
                    else if (stories.FindIndex(s => s.Story.Id == link.WorkItem.Id) is var at and >= 0 && stories[at].Via.StartsWith("via ", StringComparison.Ordinal))
                    {
                        stories[at] = (stories[at].Story, $"{stories[at].Via}, {requirement.Requirement.Code}");
                    }
                }
            }

            // R3: the open risks reached through a requirement, a story or an action that names the evidence; each once,
            // with every path, highest first
            var storyIds = stories.Select(s => s.Story.Id).ToHashSet();
            var codes = coverage.ToDictionary(r => r.Requirement.Id, r => r.Requirement.Code);
            var risks = openRisks
                .Select(risk =>
                {
                    var via = risk.Measures.Where(m => m.RequirementId is { } id && codes.ContainsKey(id)).Select(m => codes[m.RequirementId!.Value])
                        .Concat(risk.Measures.Where(m => m.WorkItemId is { } id && storyIds.Contains(id))
                            .Select(m => stories.First(s => s.Story.Id == m.WorkItemId).Story.Title))
                        .Concat(risk.Actions.SelectMany(a => a.Links).Where(l => l.WorkItemId is { } id && storyIds.Contains(id))
                            .Select(l => stories.First(s => s.Story.Id == l.WorkItemId).Story.Title))
                        .Concat(risk.Actions.Where(a => a.Links.Any(l => l.EvidenceId == row.Evidence.Id)).Select(a => $"the action \"{a.Title}\""))
                        .Distinct()
                        .ToList();
                    return (Risk: risk, Via: via);
                })
                .Where(r => r.Via.Count > 0)
                .OrderByDescending(r => r.Risk.Score)
                .ThenBy(r => r.Risk.Title, StringComparer.Ordinal)
                .Select(r => new TraceEntry(r.Risk.Id, r.Risk.Title, "via " + string.Join(", ", r.Via), r.Risk.Level.ToString()))
                .ToList();

            return new EvidenceTrace(
                risks,
                row.Links.Select(l => new TraceEntry(l.Requirement.Id, $"{l.Requirement.Code} {l.Requirement.Title}",
                    l.IsApplicable ? (l.Link.Source == Domain.Mappings.LinkSource.AiApproved ? "AI suggestion approved" : "linked") : "not applicable")).ToList(),
                stories.Select(s => new TraceEntry(s.Story.Id, s.Story.Title, s.Via, RequirementRows.StoryState(s.Story.State))).ToList());
        }
    }
}
