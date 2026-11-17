using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Web.Models
{
    // US-200: one step of following a requirement to the end. Action is what to do when it is open: a dialog on the page
    // (DialogOpen, US-166), a place to go (Href), or nothing the tracker can do (the story is closed in GitHub/Azure).
    public sealed record PathStep(string Text, bool Done, string? Missing = null, string? ActionText = null, string? Href = null, string? DialogOpen = null)
    {
        public bool HasAction => ActionText is not null && (Href is not null || DialogOpen is not null);
    }

    // US-200 R1, R2, R7: the steps of a requirement, in order: for each linked story that it is done and has its evidence,
    // then the tests, the requirement's own evidence and what waits for review. Pure, so the edge cases are tested on their own.
    public static class RequirementPath
    {
        public static IReadOnlyList<PathStep> Steps(RequirementCoverage row, IReadOnlyList<StoryEvidence> storyEvidence, string evidenceTab)
        {
            // R7: a requirement that does not apply has no steps
            if (!row.IsApplicable)
            {
                return [];
            }

            var steps = new List<PathStep>();
            var stories = row.Links.Where(l => l.WorkItem.State != WorkItemState.Removed).ToList();

            // The organisation area has no stories: evidence covers its requirements (US-142)
            if (!row.EvidenceCovers)
            {
                if (stories.Count == 0)
                {
                    steps.Add(new PathStep("Link a user story that meets the requirement", false, "No story is linked yet",
                        "Link a story", "#link-story-section", "link-story"));
                }

                foreach (var link in stories)
                {
                    var story = link.WorkItem;
                    if (link.Link.NeedsReview)
                    {
                        steps.Add(new PathStep($"{story.Title}: confirm the link", false, "The link waits for review after a new version",
                            "Review it", "#waiting-for-decision"));
                    }

                    steps.Add(new PathStep($"{story.Title}: done", story.State == WorkItemState.Closed,
                        story.State == WorkItemState.Closed ? null : $"Still open in {(story.Source == WorkItemSource.AzureDevOps ? "Azure DevOps" : "GitHub")}",
                        string.IsNullOrEmpty(story.Url) ? null : "Open the story", string.IsNullOrEmpty(story.Url) ? null : story.Url));
                    var hasEvidence = storyEvidence.Any(e => e.WorkItemId == story.Id);
                    steps.Add(new PathStep($"{story.Title}: evidence", hasEvidence, hasEvidence ? null : "No evidence shows the story is done",
                        "Link evidence to the story", evidenceTab));
                }

                // The tests that are tagged with the requirement (US-058)
                var failing = row.FailingTests;
                var passing = row.Evidence.Any(e => !e.IsExpired && e.AcceptedFrom == Domain.Mappings.EvidenceSourceKind.TestRun);
                steps.Add(new PathStep("Tests pass", failing == 0 && passing,
                    failing > 0 ? $"{failing} failing {(failing == 1 ? "test" : "tests")}" : passing ? null : "No passing test run is accepted as evidence yet",
                    "Upload test results", evidenceTab + "#test-results-upload"));
            }

            steps.Add(new PathStep("Evidence on the requirement", row.HasEvidence, row.HasEvidence ? null : "No evidence that counts",
                "Add evidence", "#add-evidence-section", "add-evidence"));

            if (row.NeedsReview && stories.All(l => !l.Link.NeedsReview))
            {
                steps.Add(new PathStep("Decide what waits for review", false, "Something was carried over from an earlier version", "Review it", "#waiting-for-decision"));
            }

            return steps;
        }

        // R4: e.g. "2 of 5 steps done"
        public static string Progress(IReadOnlyList<PathStep> steps)
            => $"{steps.Count(s => s.Done)} of {steps.Count} {(steps.Count == 1 ? "step" : "steps")} done";

        public static bool Complete(IReadOnlyList<PathStep> steps) => steps.Count > 0 && steps.All(s => s.Done);

        // R6: the next requirement after this one in the framework's order, among those the user's filter shows; null at the end
        public static RequirementCoverage? Next(FrameworkCompliance framework, Guid current, string filter)
        {
            var rows = framework.Requirements;
            var at = rows.ToList().FindIndex(r => r.Requirement.Id == current);
            return at < 0 ? null : rows.Skip(at + 1).FirstOrDefault(r => ComplianceFilters.Matches(filter, r));
        }
    }
}
