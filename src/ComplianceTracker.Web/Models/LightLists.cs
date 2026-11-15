namespace ComplianceTracker.Web.Models
{
    // US-154: what a long list shows by itself, and which evidence an address points at. Pure, so the edge cases are tested.
    public static class LightLists
    {
        // The Suggestions tab shows this many gaps per category until "Show all"
        public const int GapsShown = 15;

        // "all" on the Suggestions tab's address shows every gap
        public const string ShowAll = "all";

        public static IReadOnlyList<T> Visible<T>(IReadOnlyList<T> items, string? sub)
            => sub == ShowAll || items.Count <= GapsShown ? items : items.Take(GapsShown).ToList();

        public static bool HasMore<T>(IReadOnlyList<T> items, string? sub) => sub != ShowAll && items.Count > GapsShown;

        // The evidence an address like /Projects/Details/{id}/evidence/{evidenceId} points at; null for the list
        public static Guid? EvidenceOf(string? sub) => Guid.TryParse(sub, out var id) && id != Guid.Empty ? id : null;
    }
}
