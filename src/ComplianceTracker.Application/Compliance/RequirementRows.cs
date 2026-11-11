using System.Globalization;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Compliance
{
    // US-144: the texts of a requirement row on the Compliance tab: where each story and evidence came from, a story's
    // state, and the short summary a folded row shows. Pure functions, so every source is tested on its own.
    public static class RequirementRows
    {
        // R7: a finished story reads "Done", not "Closed"
        public static string StoryState(WorkItemState state) => state switch
        {
            WorkItemState.Open => "Open",
            WorkItemState.Closed => "Done",
            _ => "Removed"
        };

        // R2: e.g. "GitHub · linked by Anna, 03-11-2026" or "Specification · AI suggestion approved by Bo, 03-11-2026"
        public static string StorySource(LinkedWorkItem link, Func<Guid, string> nameOf)
        {
            var where = link.WorkItem.Source == WorkItemSource.Document ? "Specification" : "GitHub";
            var how = link.Link.Source == LinkSource.AiApproved ? "AI suggestion approved by" : "linked by";
            return $"{where} · {how} {nameOf(link.Link.LinkedById)}, {Date(link.Link.LinkedAt)}";
        }

        // R2: e.g. "Uploaded by Anna, 03-11-2026", "From a merged pull request · accepted by Bo, 03-11-2026",
        // "From the Documents tab (organisation)"
        public static string EvidenceSource(EvidenceItem item, Func<Guid, string> nameOf)
        {
            var evidence = item.Evidence;
            if (evidence.IsFromOrganisation)
            {
                return "From the Documents tab (organisation)";
            }

            var who = $"{nameOf(evidence.AddedById)}, {Date(evidence.AddedAt)}";
            return item.AcceptedFrom switch
            {
                EvidenceSourceKind.TestRun => $"From a test run · accepted by {who}",
                EvidenceSourceKind.PullRequest => $"From a merged pull request · accepted by {who}",
                _ => evidence.IsFile ? $"Uploaded by {who}" : $"Link added by {who}"
            };
        }

        // The items that wait for a person: AI story suggestions, suggested evidence and a "not applicable" suggestion
        public static int Waiting(RequirementCoverage row)
            => (row.IsApplicable ? row.Suggestions.Count : 0)
               + row.PendingEvidenceSuggestions.Count()
               + (row.IsApplicable && row.ApplicabilitySuggestion is not null ? 1 : 0);

        // R6, R9: "2 stories · 1 evidence" (the waiting count is shown as its own tag)
        public static string Summary(RequirementCoverage row)
            => $"{row.Links.Count} {(row.Links.Count == 1 ? "story" : "stories")} · {row.Evidence.Count} evidence";

        private static string Date(DateTime at) => at.ToLocalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    }
}
