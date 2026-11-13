using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Infrastructure.Ai;

namespace ComplianceTracker.UnitTests.Ai;

// Edge-case unit tests of US-118: search → rerank → limit, the /v1/rerank answer and the two scores. Arrange / Act / Assert.
public class RerankingTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    // A pretend reranker: a score per text, or an answer of the wrong length, or one that cannot be reached
    private sealed class Reranker : IReranker
    {
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<double>> _answer;

        public Reranker(Func<IReadOnlyList<string>, IReadOnlyList<double>> answer)
        {
            _answer = answer;
        }

        public string Name => "rerank";
        public List<int> Calls { get; } = new();

        public Task<IReadOnlyList<double>> RerankAsync(string query, IReadOnlyList<string> documents, CancellationToken cancellationToken = default)
        {
            Calls.Add(documents.Count);
            return Task.FromResult(_answer(documents));
        }

        public static Reranker Scores(Dictionary<string, double> scores) => new(d => d.Select(t => scores.GetValueOrDefault(t)).ToList());
    }

    private static (string Item, double Search)[] Searched(params (string, double)[] items) => items;

    // ---------------------------------------------------------- R5: without a reranker

    [Fact]
    public void Without_a_reranker_the_search_score_orders_and_limits_as_before()
    {
        var best = Reranking.BestAsync(null, "q", Searched(("a", 0.4), ("b", 0.9), ("c", 0.6)), t => t, 0.5, 10).Result;

        Assert.Equal([("b", 0.9, (double?)null), ("c", 0.6, null)], best.Select(b => (b.Item, b.Score, b.SearchScore)));
    }

    // ---------------------------------------------------------- R1, R2: with a reranker

    [Fact]
    public async Task The_rerank_score_decides_the_order_and_the_limit()
    {
        // Arrange: "Dark mode" is first by search, but the reranker drops it
        var reranker = Reranker.Scores(new() { ["Dark mode"] = 0.12, ["Session timeout"] = 0.86, ["MFA"] = 0.7 });

        // Act
        var best = await Reranking.BestAsync(reranker, "I.2(d)", Searched(("Dark mode", 0.58), ("Session timeout", 0.41), ("MFA", 0.5)), t => t, 0.5, 10);

        // Assert
        Assert.Equal([("Session timeout", 0.86, (double?)0.41), ("MFA", 0.7, 0.5)], best.Select(b => (b.Item, b.Score, b.SearchScore)));
    }

    [Fact]
    public async Task Only_the_searchs_best_20_are_reranked_whatever_their_search_score()
    {
        var reranker = new Reranker(d => d.Select(_ => 0.9).ToList());
        var many = Enumerable.Range(0, 30).Select(i => ($"s{i}", i / 100.0)).ToArray();

        var best = await Reranking.BestAsync(reranker, "q", many, t => t, 0.5, 50);

        Assert.Equal([20], reranker.Calls);
        Assert.Equal(20, best.Count);
        Assert.DoesNotContain(best, b => b.SearchScore < 0.10);
    }

    [Fact]
    public async Task At_most_take_are_kept_and_a_tie_goes_to_the_better_search_score()
    {
        var reranker = new Reranker(d => d.Select(_ => 0.8).ToList());

        var best = await Reranking.BestAsync(reranker, "q", Searched(("a", 0.3), ("b", 0.6), ("c", 0.5)), t => t, 0.5, 2);

        Assert.Equal(["b", "c"], best.Select(b => b.Item));
    }

    [Theory]
    [InlineData(1.7, 1.0)]
    [InlineData(-0.2, 0.0)]
    [InlineData(double.NaN, 0.0)]
    public async Task A_rerank_score_out_of_range_is_kept_within_0_to_1(double answer, double expected)
    {
        var reranker = new Reranker(d => d.Select(_ => answer).ToList());

        var best = await Reranking.BestAsync(reranker, "q", Searched(("a", 0.9)), t => t, 0, 10);

        Assert.Equal(expected, best.Single().Score);
    }

    [Fact]
    public async Task No_candidates_means_no_call_to_the_reranker()
    {
        var reranker = new Reranker(d => d.Select(_ => 0.9).ToList());

        var best = await Reranking.BestAsync(reranker, "q", Searched(), t => t, 0.5, 10);

        Assert.Empty(best);
        Assert.Empty(reranker.Calls);
    }

    [Fact]
    public async Task An_answer_of_the_wrong_length_or_an_unreachable_reranker_stops_with_a_message()
    {
        var wrong = new Reranker(_ => [0.9]);
        var down = new Reranker(_ => throw new AiUnavailableException("Ranking is unavailable: the model rerank cannot be reached"));

        var lengthError = await Assert.ThrowsAsync<DomainException>(() => Reranking.BestAsync(wrong, "q", Searched(("a", 0.9), ("b", 0.8)), t => t, 0.5, 10));
        var downError = await Assert.ThrowsAsync<DomainException>(() => Reranking.BestAsync(down, "q", Searched(("a", 0.9)), t => t, 0.5, 10));

        Assert.Equal("The reranker rerank did not return one score per candidate", lengthError.Message);
        Assert.Equal("Ranking is unavailable: the model rerank cannot be reached", downError.Message);
    }

    // ---------------------------------------------------------- The /v1/rerank answer

    [Fact]
    public void An_answer_sorted_by_score_is_put_back_in_the_documents_order()
    {
        var results = new List<OpenAiCompatibleReranker.RerankResult> { new(2, 0.9, null), new(0, 0.4, null), new(1, null, 0.2) };

        Assert.Equal([0.4, 0.2, 0.9], OpenAiCompatibleReranker.Scores(results, 3, "m"));
    }

    [Theory]
    [InlineData(new[] { 0, 1 })]       // one missing
    [InlineData(new[] { 0, 0, 1 })]    // one twice
    [InlineData(new[] { 0, 1, 3 })]    // out of range
    public void An_answer_without_exactly_one_score_per_document_is_refused(int[] indexes)
    {
        var results = indexes.Select(i => new OpenAiCompatibleReranker.RerankResult(i, 0.5, null)).ToList();

        Assert.Throws<AiUnavailableException>(() => OpenAiCompatibleReranker.Scores(results, 3, "m"));
    }

    [Fact]
    public void The_Ranking_task_is_off_without_its_own_model()
    {
        Assert.Equal(AiTaskNames.RankingOff, AiTaskNames.DefaultModel(AiTask.Ranking, "embed", "chat"));
        Assert.Equal("embed", AiTaskNames.DefaultModel(AiTask.Embedding, "embed", "chat"));
        Assert.Equal("chat", AiTaskNames.DefaultModel(AiTask.Rationale, "embed", "chat"));
    }

    // ---------------------------------------------------------- R4: both scores are stored

    [Fact]
    public void A_reranked_suggestion_keeps_its_search_score_and_the_ranking_model()
    {
        var suggestion = MappingSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.86, 1, "embed", "v1", Monday, 0.5)
            .Reranked(0.61234, "bge-reranker");

        Assert.Equal((0.86, (double?)0.6123, "bge-reranker"), (suggestion.Score, suggestion.SearchScore, suggestion.RankingModel));
    }

    [Fact]
    public void A_suggestion_without_a_reranker_has_no_search_score()
    {
        var suggestion = EvidenceLinkSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.7, 1, "embed", "v1", Monday, 0.5)
            .Reranked(null, null);

        Assert.Equal(((double?)null, (string?)null), (suggestion.SearchScore, suggestion.RankingModel));
    }
}
