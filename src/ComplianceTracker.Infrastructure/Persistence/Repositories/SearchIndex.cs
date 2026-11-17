using ComplianceTracker.Application.Search;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-100: PostgreSQL full-text search with the English configuration (R4: "logging" finds "log"), plus a plain
    // "contains" for codes and ids such as "I.2(d)" or "US-004", which the full-text search splits up. The term is always
    // a parameter (R5); EF Core sends it as one. The GIN indexes are in the configurations (migration Search).
    public class SearchIndex : ISearchIndex
    {
        public const string Language = "english";

        private readonly ComplianceTrackerDbContext _db;

        public SearchIndex(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchKind kind, string term, Guid organizationId,
            IReadOnlyCollection<Guid> projectIds, int take, CancellationToken cancellationToken = default)
        {
            var like = SearchRules.Like(term);
            switch (kind)
            {
                case SearchKind.Requirement:
                {
                    // R2: the active versions of the organisation's own frameworks and the built-in ones it has switched on
                    var versions = _db.FrameworkVersions.Where(v => v.Status == FrameworkVersionStatus.Active
                        && _db.Frameworks.Any(f => f.Id == v.FrameworkId
                            && (f.OwnerOrganizationId == organizationId
                                || (f.OwnerOrganizationId == null && _db.OrganizationFrameworks.Any(s =>
                                    s.OrganizationId == organizationId && s.DisabledAt == null && s.FrameworkVersionId == v.Id)))));
                    return await _db.Requirements
                        .Where(r => versions.Any(v => v.Id == r.FrameworkVersionId))
                        .Where(r => EF.Functions.ToTsVector(Language, r.Code + " " + r.Title + " " + r.Description).Matches(EF.Functions.WebSearchToTsQuery(Language, term))
                            || EF.Functions.ILike(r.Code, like, "\\") || EF.Functions.ILike(r.Title, like, "\\"))
                        .Join(_db.FrameworkVersions, r => r.FrameworkVersionId, v => v.Id, (r, v) => new { r, v })
                        .Join(_db.Frameworks, x => x.v.FrameworkId, f => f.Id, (x, f) => new { x.r, x.v, f })
                        .OrderBy(x => x.f.Name).ThenBy(x => x.r.Position)
                        .Take(take)
                        .Select(x => new SearchHit(SearchKind.Requirement, x.r.Id, x.r.Code + " " + x.r.Title, x.f.Name, null, x.v.Id.ToString()))
                        .ToListAsync(cancellationToken);
                }

                case SearchKind.WorkItem:
                    return await _db.WorkItems
                        .Where(w => projectIds.Contains(w.ProjectId) && w.State != WorkItemState.Removed)
                        .Where(w => EF.Functions.ToTsVector(Language, w.ExternalReference + " " + w.Title + " " + w.Rules + " " + w.Description).Matches(EF.Functions.WebSearchToTsQuery(Language, term))
                            || EF.Functions.ILike(w.ExternalReference, like, "\\") || EF.Functions.ILike(w.Title, like, "\\"))
                        .OrderBy(w => w.Title)
                        .Take(take)
                        .Select(w => new SearchHit(SearchKind.WorkItem, w.Id, w.Title, w.ExternalReference, w.ProjectId, w.Url))
                        .ToListAsync(cancellationToken);

                case SearchKind.Evidence:
                    return await _db.Evidence
                        .Where(e => projectIds.Contains(e.ProjectId) && e.RemovedAt == null)
                        .Where(e => EF.Functions.ToTsVector(Language, e.Title).Matches(EF.Functions.WebSearchToTsQuery(Language, term)) || EF.Functions.ILike(e.Title, like, "\\"))
                        .OrderBy(e => e.Title)
                        .Take(take)
                        .Select(e => new SearchHit(SearchKind.Evidence, e.Id, e.Title, null, e.ProjectId, ""))
                        .ToListAsync(cancellationToken);

                case SearchKind.Risk:
                    return await _db.Risks
                        .Where(r => projectIds.Contains(r.ProjectId))
                        .Where(r => EF.Functions.ToTsVector(Language, r.Title + " " + r.Description).Matches(EF.Functions.WebSearchToTsQuery(Language, term)) || EF.Functions.ILike(r.Title, like, "\\"))
                        .OrderBy(r => r.Title)
                        .Take(take)
                        .Select(r => new SearchHit(SearchKind.Risk, r.Id, r.Title, null, r.ProjectId, ""))
                        .ToListAsync(cancellationToken);

                default:
                    return await _db.Projects
                        .Where(p => projectIds.Contains(p.Id))
                        .Where(p => EF.Functions.ToTsVector(Language, p.Name).Matches(EF.Functions.WebSearchToTsQuery(Language, term)) || EF.Functions.ILike(p.Name, like, "\\"))
                        .OrderBy(p => p.Name)
                        .Take(take)
                        .Select(p => new SearchHit(SearchKind.Project, p.Id, p.Name, null, p.Id, ""))
                        .ToListAsync(cancellationToken);
            }
        }
    }
}
