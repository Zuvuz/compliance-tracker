using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // US-205 R2, R10: one suggestion's check, as _PassageCheck.cshtml shows it
    public sealed record PassageCheckView(PassageVerdict? Verdict, string? Passage, string? Missing, string? Model)
    {
        public bool IsChecked => Model is not null && Verdict is not null && Passage is not null;

        public static PassageCheckView Of(DocumentLinkSuggestion suggestion)
            => new(suggestion.CheckVerdict, suggestion.CheckPassage, suggestion.CheckMissing, suggestion.CheckModel);

        public static PassageCheckView Of(EvidenceLinkSuggestion suggestion)
            => new(suggestion.CheckVerdict, suggestion.CheckPassage, suggestion.CheckMissing, suggestion.CheckModel);
    }
}
