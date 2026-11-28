using ComplianceTracker.Application.Projects.Tabs;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Risks
{
    // US-229: the Risks tab's filters (US-042 R8, US-147, US-159 R4, US-203), already read from the address.
    // Status and Level are compared with the risk's text; null shows every risk that is not closed, at every level.
    public record RiskTabRequest(
        string? Status = null,
        string? Level = null,
        bool OnlyReviewDue = false,
        bool OnlyNothingBehind = false,
        (int Likelihood, int Impact)? Cell = null,
        bool MatrixAfter = false,
        Guid? RiskInPanel = null);

    // US-229: what the Risks tab shows. All: every risk (the matrix counts the open ones); Shown: the register after the
    // filters; RiskInPanel: the risk opened in the side panel, when it is one of the project's (US-147 R4).
    public record RiskTabDto(
        IReadOnlyList<Risk> All,
        IReadOnlyList<Risk> Shown,
        IReadOnlyDictionary<Guid, RiskBacking> Backing,
        IReadOnlyList<RiskDraft> Drafts,
        Guid? RiskInPanel,
        DateTime Now);

    public class RiskTabLoader : IProjectTabLoader<RiskTabRequest, RiskTabDto>
    {
        private readonly RiskQueries _risks;
        private readonly RiskBackingQueries _backing;
        private readonly RiskDraftService _drafts;
        private readonly TimeProvider _clock;

        public RiskTabLoader(RiskQueries risks, RiskBackingQueries backing, RiskDraftService drafts, TimeProvider clock)
        {
            _risks = risks;
            _backing = backing;
            _drafts = drafts;
            _clock = clock;
        }

        // Null for another organisation's project
        public async Task<RiskTabDto?> LoadAsync(Guid projectId, RiskTabRequest request, CancellationToken cancellationToken = default)
        {
            if (await _risks.ForProjectAsync(projectId, cancellationToken) is not { } all)
            {
                return null;
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            // US-159 R5: the links behind every risk in a fixed number of queries
            var backing = RiskBackings.For(all, await _backing.LinksAsync(projectId, cancellationToken));
            return new RiskTabDto(
                all,
                RiskRegister.Filter(all, backing, request, now),
                backing,
                await _drafts.OpenAsync(projectId, cancellationToken), // US-112
                request.RiskInPanel is { } inPanel && all.Any(r => r.Id == inPanel) ? inPanel : null,
                now);
        }
    }

    // US-042 R8, US-147, US-159 R4, US-203 R2: the register after the filters
    public static class RiskRegister
    {
        public static IReadOnlyList<Risk> Filter(
            IEnumerable<Risk> risks, IReadOnlyDictionary<Guid, RiskBacking> backing, RiskTabRequest request, DateTime now)
            => risks
                // R7: closed risks only when asked for
                .Where(r => string.IsNullOrEmpty(request.Status)
                    ? r.Status != RiskStatus.Closed
                    : r.DisplayStatus.Equals(request.Status, StringComparison.OrdinalIgnoreCase))
                .Where(r => !request.OnlyNothingBehind || (backing.TryGetValue(r.Id, out var b) && b.NothingBehind))
                .Where(r => string.IsNullOrEmpty(request.Level) || r.Level.ToString().Equals(request.Level, StringComparison.OrdinalIgnoreCase))
                .Where(r => !request.OnlyReviewDue || r.IsReviewDue(now))
                // US-203 R2: a cell of the matrix after the measures filters by the residual risk
                .Where(r => request.Cell is not { } c || (request.MatrixAfter
                    ? r.LikelihoodAfter == c.Likelihood && r.ImpactAfter == c.Impact
                    : r.Likelihood == c.Likelihood && r.Impact == c.Impact))
                // US-147 R1: highest level first, then by title
                .OrderByDescending(r => r.Score).ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
