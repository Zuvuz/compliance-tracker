using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Ai
{
    // US-106: storage for the AI's story drafts (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IStoryDraftRepository
    {
        Task<IReadOnlyList<StoryDraft>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(StoryDraft draft, CancellationToken cancellationToken = default);
    }

    // US-106 R12: storage for the AI's measure suggestions for risks
    public interface IRiskMeasureSuggestionRepository
    {
        Task<IReadOnlyList<RiskMeasureSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<RiskMeasureSuggestion> suggestions, CancellationToken cancellationToken = default);
    }
}
