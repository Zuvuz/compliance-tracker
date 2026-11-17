using ComplianceTracker.Application.Search;

namespace ComplianceTracker.Web.Models
{
    // US-100: the results, grouped by kind (R3), and the names of the projects the hits belong to
    public sealed record SearchViewModel(SearchResult Result, IReadOnlyDictionary<Guid, string> ProjectNames)
    {
        public string? ProjectOf(SearchHit hit)
            => hit.ProjectId is { } id && ProjectNames.TryGetValue(id, out var name) ? name : null;
    }
}
