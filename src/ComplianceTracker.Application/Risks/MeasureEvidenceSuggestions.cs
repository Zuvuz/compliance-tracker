using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Risks
{
    // US-161 R1: one piece of the project's evidence suggested for a suggested measure, with its score
    public sealed record SuggestedMeasureEvidence(Evidence Evidence, double Score);

    // The suggestions per measure suggestion, or why there are none (R5)
    public sealed record MeasureEvidenceView(IReadOnlyDictionary<Guid, IReadOnlyList<SuggestedMeasureEvidence>> BySuggestion, string? Unavailable)
    {
        public static readonly MeasureEvidenceView None = new(new Dictionary<Guid, IReadOnlyList<SuggestedMeasureEvidence>>(), null);

        public IReadOnlyList<SuggestedMeasureEvidence> For(Guid suggestionId)
            => BySuggestion.TryGetValue(suggestionId, out var found) ? found : [];
    }

    // US-161: while a person approves a measure the AI suggested for a risk, the AI suggests which of the project's existing
    // evidence proves it: ranked against the measure's requirement (or story) with the embeddings and the reranker used for
    // evidence (US-059, US-118). Nothing is stored; a person picks one when approving (AI Act Art. 14).
    public class MeasureEvidenceSuggestions
    {
        public const int PerMeasure = 3;
        public const string NotAvailable = "No suggestions: the AI is not available";

        private readonly IEmbeddingModel _embeddings;
        private readonly IReranker? _reranker;
        private readonly IAiLimits _limits;

        public MeasureEvidenceSuggestions(IAiModels models, IAiLimits limits)
        {
            _embeddings = models.Embedding(AiTask.Embedding);
            _reranker = models.Reranker();
            _limits = limits;
        }

        public async Task<MeasureEvidenceView> ForAsync(IReadOnlyList<RiskMeasureSuggestion> pending, IReadOnlyList<EvidenceRow> evidence,
            ProjectCompliance? compliance, DateOnly today, CancellationToken cancellationToken = default)
        {
            var measures = pending.Where(s => s.IsPending).Select(s => (Suggestion: s, Query: QueryOf(s, compliance))).Where(m => m.Query is not null).ToList();
            var usable = evidence.Where(e => e.Evidence.IsActive && !e.Evidence.IsExpiredOn(today)).ToList(); // R2
            if (measures.Count == 0 || usable.Count == 0)
            {
                return MeasureEvidenceView.None;
            }

            try
            {
                var texts = usable.Select(e => EvidenceService.EvidenceText(e.Evidence, null)).ToList();
                var vectors = await _embeddings.EmbedAsync([.. measures.Select(m => m.Query!), .. texts], cancellationToken);
                var result = new Dictionary<Guid, IReadOnlyList<SuggestedMeasureEvidence>>();
                for (var i = 0; i < measures.Count; i++)
                {
                    var (suggestion, query) = measures[i];
                    var candidates = Candidates(suggestion, usable)
                        .Select(e => (Item: e, Search: Similarity.Cosine(vectors[i], vectors[measures.Count + usable.IndexOf(e)])));
                    var best = await Reranking.BestAsync(_reranker, query!, candidates, e => EvidenceService.EvidenceText(e.Evidence, null),
                        _limits.MinimumScoreForEvidence, PerMeasure, cancellationToken); // R4
                    result[suggestion.Id] = best.Select(b => new SuggestedMeasureEvidence(b.Item.Evidence, b.Score)).ToList();
                }

                return new MeasureEvidenceView(result, null);
            }
            catch (AiUnavailableException)
            {
                return new MeasureEvidenceView(new Dictionary<Guid, IReadOnlyList<SuggestedMeasureEvidence>>(), NotAvailable); // R5
            }
        }

        // R2: evidence already on the measure's requirement (or story) is not suggested again
        public static IEnumerable<EvidenceRow> Candidates(RiskMeasureSuggestion suggestion, IEnumerable<EvidenceRow> evidence)
            => evidence.Where(e => suggestion.RequirementId is { } requirementId
                ? e.Links.All(l => l.Requirement.Id != requirementId)
                : e.Stories.All(s => s.Story.Id != suggestion.WorkItemId));

        // The text the evidence is compared with: the requirement's, or the story's; null when it is no longer in the project
        public static string? QueryOf(RiskMeasureSuggestion suggestion, ProjectCompliance? compliance)
        {
            if (compliance is null)
            {
                return null;
            }

            if (suggestion.RequirementId is { } requirementId)
            {
                return compliance.Frameworks.SelectMany(f => f.Requirements).FirstOrDefault(r => r.Requirement.Id == requirementId) is { } row
                    ? MappingSuggestionService.RequirementText(row.Requirement)
                    : null;
            }

            return compliance.WorkItems.FirstOrDefault(w => w.Id == suggestion.WorkItemId) is { } story ? MappingSuggestionService.StoryText(story) : null;
        }
    }
}
