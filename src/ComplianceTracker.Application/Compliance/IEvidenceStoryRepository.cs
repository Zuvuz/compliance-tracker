using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Compliance
{
    // US-146: storage for the links between evidence and user stories, and the AI's suggestions for them (implemented in
    // Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IEvidenceStoryRepository
    {
        Task<IReadOnlyList<EvidenceStoryLink>> ListActiveLinksAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddLinkAsync(EvidenceStoryLink link, CancellationToken cancellationToken = default);

        // Every suggestion of the project, in every status
        Task<IReadOnlyList<EvidenceStorySuggestion>> ListSuggestionsAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddSuggestionsAsync(IEnumerable<EvidenceStorySuggestion> suggestions, CancellationToken cancellationToken = default);
    }
}
