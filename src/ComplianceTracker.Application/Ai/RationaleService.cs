using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.WorkItems;

namespace ComplianceTracker.Application.Ai
{
    // Step 2 of 2 (US-020 R4): the chat model writes a short rationale for each pending suggestion.
    // Run by a background job (no user), because a small model on a laptop CPU takes seconds per rationale.
    public class RationaleService
    {
        private readonly IMappingSuggestionRepository _suggestions;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly IRationaleWriter _writer;
        private readonly IWorkItemNotifier _notifier;
        private readonly IUnitOfWork _unitOfWork;

        public RationaleService(
            IMappingSuggestionRepository suggestions,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            IRationaleWriter writer,
            IWorkItemNotifier notifier,
            IUnitOfWork unitOfWork)
        {
            _suggestions = suggestions;
            _frameworks = frameworks;
            _workItems = workItems;
            _writer = writer;
            _notifier = notifier;
            _unitOfWork = unitOfWork;
        }

        // Writes rationales for up to "take" suggestions, best ranked first. Returns how many were written.
        // Each is saved at once, so the page can show them one by one. Throws AiUnavailableException if the model is down.
        public async Task<int> WriteMissingAsync(int take, CancellationToken cancellationToken = default)
        {
            var written = 0;
            foreach (var suggestion in await _suggestions.ListWithoutRationaleAsync(take, cancellationToken))
            {
                var requirement = await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken);
                var story = (await _workItems.ListAsync(suggestion.ProjectId, cancellationToken)).FirstOrDefault(w => w.Id == suggestion.WorkItemId);
                if (requirement is null || story is null)
                {
                    suggestion.SetRationale("(The story or the requirement no longer exists.)", _writer.Name);
                }
                else
                {
                    var prompt = RationalePrompt.For(story.Title, MappingSuggestionService.StoryText(story),
                        requirement.Code, MappingSuggestionService.RequirementText(requirement));
                    suggestion.SetRationale(await _writer.WriteAsync(prompt, cancellationToken), _writer.Name);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _notifier.WorkItemsChangedAsync(suggestion.ProjectId, cancellationToken);
                written++;
            }

            return written;
        }
    }
}
