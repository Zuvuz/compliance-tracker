using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Ai
{
    // US-205 R9: how good the document suggestions were, measured on people's decisions. Pure, so it is tested on its own.
    public static class PassageCheckEvaluation
    {
        public sealed record Result(string Model, double? CheckedRight, double? NotCheckedRight, double? Found);

        // CheckedRight: approved / decided among those the model checked; NotCheckedRight: the same for those made without the
        // check (the "before"); Found: approved / (approved + links made by hand). Model: the model that checked most of them.
        public static Result Measure(IReadOnlyCollection<DocumentLinkSuggestion> decided, int linkedByHand)
        {
            static double? Share(IReadOnlyCollection<DocumentLinkSuggestion> suggestions)
                => suggestions.Count == 0 ? null : (double)suggestions.Count(s => s.Status == SuggestionStatus.Approved) / suggestions.Count;

            var isChecked = decided.Where(s => s.IsChecked).ToList();
            var notChecked = decided.Where(s => !s.IsChecked).ToList();
            var approved = decided.Count(s => s.Status == SuggestionStatus.Approved);
            var model = isChecked.GroupBy(s => s.CheckModel!).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "not checked";
            var all = approved + Math.Max(0, linkedByHand);
            return new Result(model, Share(isChecked), Share(notChecked), all == 0 ? null : (double)approved / all);
        }

        // The documents' active links that no approved suggestion made: right answers the AI did not find
        public static int LinkedByHand(IEnumerable<OrganisationDocument> documents, IReadOnlyCollection<DocumentLinkSuggestion> suggestions)
            => documents.SelectMany(d => d.ActiveLinks.Select(l => (d.Id, l.FrameworkId, l.RequirementCode)))
                .Count(link => !suggestions.Any(s => s.DocumentId == link.Id && s.Status == SuggestionStatus.Approved && s.IsFor(link.FrameworkId, link.RequirementCode)));
    }
}
