using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;

namespace ComplianceTracker.Application.Search
{
    // US-100 R1: what the search covers
    public enum SearchKind
    {
        Requirement,
        WorkItem,
        Evidence,
        Risk,
        Project
    }

    // One hit: what it is, its title, where it belongs (a project, or a framework for a requirement) and where it opens.
    // ProjectId is null for a requirement, which belongs to a framework.
    public sealed record SearchHit(SearchKind Kind, Guid Id, string Title, string? Detail, Guid? ProjectId, string Where);

    // The hits of one kind: at most Limit, and whether there are more (R6)
    public sealed record SearchGroup(SearchKind Kind, IReadOnlyList<SearchHit> Hits, bool HasMore);

    public sealed record SearchResult(string Term, IReadOnlyList<SearchGroup> Groups)
    {
        public bool IsEmpty => Groups.All(g => g.Hits.Count == 0);
    }

    // US-100: the search in the database (implemented in Infrastructure with PostgreSQL full-text search). The term is
    // always a parameter (R5). Only what belongs to the given projects and frameworks is searched (R2).
    public interface ISearchIndex
    {
        Task<IReadOnlyList<SearchHit>> SearchAsync(SearchKind kind, string term, Guid organizationId,
            IReadOnlyCollection<Guid> projectIds, int take, CancellationToken cancellationToken = default);
    }

    // US-100: the rules of the search. Pure, so the edge cases are tested on their own.
    public static class SearchRules
    {
        // R6: at most 50 hits per kind; "Show more" shows up to this many
        public const int Limit = 50;
        public const int MoreLimit = 500;
        public const int MaxTermLength = 200;

        // A term is trimmed, its inner spaces made one, and cut to 200 characters; null when there is nothing to search for
        public static string? Clean(string? term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return null;
            }

            var clean = string.Join(' ', term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            clean = clean.Length > MaxTermLength ? clean[..MaxTermLength].TrimEnd() : clean;
            return clean.Length == 0 ? null : clean;
        }

        // For ILIKE: % and _ and \ in the term are searched for as they are
        public static string Like(string term)
            => "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        public static string KindName(SearchKind kind) => kind switch
        {
            SearchKind.Requirement => "Requirements",
            SearchKind.WorkItem => "Work items",
            SearchKind.Evidence => "Evidence",
            SearchKind.Risk => "Risks",
            _ => "Projects"
        };
    }

    // US-100: searches the signed-in member's own organisation: its projects (only the one a limited member may see,
    // US-182) and the frameworks it uses (its own and the built-in ones it has switched on). R2: the organisation check
    // comes first; without an organisation there is nothing to search.
    public class SearchService
    {
        private readonly ISearchIndex _index;
        private readonly IProjectRepository _projects;
        private readonly CurrentMember _member;

        public SearchService(ISearchIndex index, IProjectRepository projects, CurrentMember member)
        {
            _index = index;
            _projects = projects;
            _member = member;
        }

        // more: the one kind to show more of (R6)
        public async Task<SearchResult> SearchAsync(string? term, SearchKind? more = null, CancellationToken cancellationToken = default)
        {
            var clean = SearchRules.Clean(term);
            if (clean is null || await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return new SearchResult(clean ?? "", []);
            }

            var only = await _member.OnlyProjectAsync(cancellationToken);
            var projectIds = (await _projects.ListAsync(organization.Id, cancellationToken))
                .Where(p => only is null || p.Id == only).Select(p => p.Id).ToList();

            var groups = new List<SearchGroup>();
            foreach (var kind in Enum.GetValues<SearchKind>())
            {
                var limit = kind == more ? SearchRules.MoreLimit : SearchRules.Limit;
                var hits = await _index.SearchAsync(kind, clean, organization.Id, projectIds, limit + 1, cancellationToken);
                groups.Add(new SearchGroup(kind, hits.Take(limit).ToList(), hits.Count > limit));
            }

            return new SearchResult(clean, groups);
        }
    }
}
