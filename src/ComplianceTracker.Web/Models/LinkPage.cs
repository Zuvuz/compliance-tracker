using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Web.Models
{
    // US-220: the Link page. A compliance requirement is chosen on the left; the middle column shows its product
    // requirements and the right column its evidence: what is linked first, then the AI's suggestions, then the rest (R1).
    // Built from the data the Compliance tab already reads, so the page makes a fixed number of queries.
    public static class LinkMarks
    {
        public const string Covered = "covered";
        public const string Partly = "partly";
        public const string NotCovered = "not-covered";
        public const string NotApplicable = "not-applicable";

        // R4: covered when a product requirement covers it and evidence that counts proves it; partly when only one of
        // the two is there (or a link waits for review); not applicable shows its reason instead (R5)
        public static string Of(RequirementCoverage row)
        {
            if (!row.IsApplicable)
            {
                return NotApplicable;
            }

            var covered = row.IsCovered;
            var evidence = row.HasEvidence;
            return covered && evidence ? Covered
                : covered || evidence || row.IsCoveredNeedingReview ? Partly
                : NotCovered;
        }

        public static string Text(string mark) => mark switch
        {
            Covered => "Covered",
            Partly => "Partly",
            NotApplicable => "Not applicable",
            _ => "Not covered"
        };
    }

    // One row of the left column: the requirement, its mark, and whether the AI has suggestions for it
    public record LinkRequirementRow(RequirementCoverage Row, string Mark, bool HasSuggestions);

    public record LinkPage(
        FrameworkCompliance Framework,
        string Sub,
        string Filter,
        string? Search,
        IReadOnlyList<LinkRequirementRow> Requirements,
        RequirementCoverage? Chosen,
        IReadOnlyList<LinkedWorkItem> LinkedStories,
        IReadOnlyList<SuggestedWorkItem> SuggestedStories,
        IReadOnlyList<WorkItem> OtherStories,
        int MoreStories,
        IReadOnlyList<EvidenceItem> LinkedEvidence,
        IReadOnlyList<SuggestedEvidence> SuggestedEvidence,
        IReadOnlyList<Evidence> OtherEvidence,
        int MoreEvidence)
    {
        // The rest of a column is cut off here; a search narrows it (a requirement may have hundreds of candidates)
        public const int MaxOthers = 50;

        // R6: the page's filters, shared as ?show=…; the page opens on "Not covered" (the user, 2026-11-25)
        public static readonly IReadOnlyList<(string Slug, string Title)> Filters =
        [
            (ComplianceFilters.NotCovered, "Not covered"),
            (ComplianceFilters.HasSuggestions, "Has suggestions"),
            (ComplianceFilters.All, "All")
        ];

        public static string FilterOf(string? show)
            => Filters.Select(f => f.Slug).FirstOrDefault(f => string.Equals(f, show?.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? ComplianceFilters.NotCovered;

        // R5: links can be made only while the requirement applies
        public bool CanLinkChosen => Chosen is { IsApplicable: true };

        // "Has suggestions": the AI suggests a product requirement or evidence for a requirement that applies
        public static bool Suggests(RequirementCoverage row) => row.IsApplicable && (row.Suggestions.Count > 0 || row.SuggestedEvidence.Count > 0);

        // The framework is the sub-address (e.g. "cra"); none chosen is the first. An unknown framework is null: not found.
        // The requirement is the code in the address, whatever its case; none chosen is the first one shown.
        public static LinkPage? Build(
            ProjectCompliance compliance, IReadOnlyList<Evidence> evidence,
            string? sub, string? req, string? show, string? search, string? storySearch, string? evidenceSearch)
        {
            var framework = string.IsNullOrEmpty(sub)
                ? compliance.Frameworks.FirstOrDefault()
                : compliance.Frameworks.FirstOrDefault(f => FrameworkNames.Slug(f.Framework.Name) == sub);
            if (framework is null)
            {
                return null;
            }

            var filter = FilterOf(show);
            var words = Clean(search);
            var rows = framework.Requirements
                .Where(r => filter == ComplianceFilters.HasSuggestions ? Suggests(r) : ComplianceFilters.Matches(filter, r))
                .Where(r => words is null || Contains(r.Requirement.Code, words) || Contains(r.Requirement.Title, words))
                .Select(r => new LinkRequirementRow(r, LinkMarks.Of(r), Suggests(r)))
                .ToList();

            var code = RequirementPages.Clean(req);
            var chosen = code is null
                ? rows.FirstOrDefault()?.Row
                : framework.Requirements.FirstOrDefault(r => string.Equals(r.Requirement.Code, code, StringComparison.OrdinalIgnoreCase));

            var slug = FrameworkNames.Slug(framework.Framework.Name);
            if (chosen is null)
            {
                return new LinkPage(framework, slug, filter, words, rows, null, [], [], [], 0, [], [], [], 0);
            }

            // The middle column: linked, then the AI's pending suggestions (best first, as on the Compliance tab), then the rest
            var linkedStories = chosen.Links.OrderBy(l => l.WorkItem.Title, StringComparer.OrdinalIgnoreCase).ToList();
            var suggestedStories = chosen.IsApplicable
                ? chosen.Suggestions.Where(s => linkedStories.All(l => l.WorkItem.Id != s.WorkItem.Id)).ToList()
                : [];
            var storyWords = Clean(storySearch);
            var otherStories = compliance.WorkItems
                .Where(w => w.State != WorkItemState.Removed)
                .Where(w => linkedStories.All(l => l.WorkItem.Id != w.Id) && suggestedStories.All(s => s.WorkItem.Id != w.Id))
                .Where(w => storyWords is null || Contains(w.Title, storyWords))
                .OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // The right column: the same for the project's evidence
            var linkedEvidence = chosen.Evidence;
            var suggestedEvidence = chosen.IsApplicable
                ? chosen.SuggestedEvidence.Where(s => linkedEvidence.All(e => e.Evidence.Id != s.Evidence.Id)).ToList()
                : [];
            var evidenceWords = Clean(evidenceSearch);
            var otherEvidence = evidence
                .Where(e => linkedEvidence.All(l => l.Evidence.Id != e.Id) && suggestedEvidence.All(s => s.Evidence.Id != e.Id))
                .Where(e => evidenceWords is null || Contains(e.Title, evidenceWords))
                .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new LinkPage(framework, slug, filter, words, rows, chosen,
                linkedStories, suggestedStories, otherStories.Take(MaxOthers).ToList(), Math.Max(0, otherStories.Count - MaxOthers),
                linkedEvidence, suggestedEvidence, otherEvidence.Take(MaxOthers).ToList(), Math.Max(0, otherEvidence.Count - MaxOthers));
        }

        private static string? Clean(string? text)
        {
            var trimmed = text?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private static bool Contains(string? text, string words) => text?.Contains(words, StringComparison.OrdinalIgnoreCase) == true;
    }
}
