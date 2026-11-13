using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Application.Ai
{
    // US-118: reads a query together with each candidate and scores how well they match (a cross-encoder), which is
    // more precise than comparing two vectors. Implemented in Infrastructure against the organisation's own AI server.
    public interface IReranker
    {
        // The model's name as stored with every suggestion, e.g. "bge-reranker-v2-m3"
        string Name { get; }

        // One score between 0 and 1 per document, in the same order. Throws AiUnavailableException when it cannot be reached.
        Task<IReadOnlyList<double>> RerankAsync(string query, IReadOnlyList<string> documents, CancellationToken cancellationToken = default);
    }

    // A candidate after ranking: its score, and the search score it had before reranking (null without a reranker)
    public record Ranked<T>(T Item, double Score, double? SearchScore);

    // US-118: search → rerank → limit. The rationale is written afterwards, only for those that are kept (R3).
    public static class Reranking
    {
        // R1: how many of the search's best candidates the reranker reads
        public const int Candidates = 20;

        public static async Task<IReadOnlyList<Ranked<T>>> BestAsync<T>(IReranker? reranker, string query, IEnumerable<(T Item, double Search)> scored,
            Func<T, string> text, double minimum, int take, CancellationToken cancellationToken = default)
        {
            // R5: without a reranker, today's behaviour: the search score is the score
            if (reranker is null)
            {
                return scored.Where(c => c.Search >= minimum).OrderByDescending(c => c.Search).Take(take)
                    .Select(c => new Ranked<T>(c.Item, c.Search, null)).ToList();
            }

            // R1: the search's best 20, whatever their search score
            var top = scored.OrderByDescending(c => c.Search).Take(Candidates).ToList();
            if (top.Count == 0)
            {
                return [];
            }

            IReadOnlyList<double> scores;
            try
            {
                scores = await reranker.RerankAsync(query, top.Select(c => text(c.Item)).ToList(), cancellationToken);
            }
            catch (AiUnavailableException error)
            {
                // Shown on the page like a broken rule; nothing is saved
                throw new DomainException(error.Message);
            }

            if (scores.Count != top.Count)
            {
                throw new DomainException($"The reranker {reranker.Name} did not return one score per candidate");
            }

            // R2: ordered and limited by the rerank score; the search score breaks a tie
            return top.Select((c, i) => (Candidate: c, Score: double.IsNaN(scores[i]) ? 0 : Math.Clamp(scores[i], 0, 1)))
                .Where(x => x.Score >= minimum)
                .OrderByDescending(x => x.Score).ThenByDescending(x => x.Candidate.Search)
                .Take(take)
                .Select(x => new Ranked<T>(x.Candidate.Item, x.Score, x.Candidate.Search))
                .ToList();
        }
    }
}
