using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
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
        private readonly IRationaleWriter _evidenceWriter;
        private readonly IWorkItemNotifier _notifier;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEvidenceLinkSuggestionRepository _evidenceSuggestions;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceTextReader _evidenceText;

        public RationaleService(
            IMappingSuggestionRepository suggestions,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            IAiModels models,
            IWorkItemNotifier notifier,
            IUnitOfWork unitOfWork,
            IEvidenceLinkSuggestionRepository evidenceSuggestions,
            IEvidenceRepository evidence,
            IEvidenceTextReader evidenceText)
        {
            _evidenceSuggestions = evidenceSuggestions;
            _evidence = evidence;
            _evidenceText = evidenceText;
            _suggestions = suggestions;
            _frameworks = frameworks;
            _workItems = workItems;
            _writer = models.Writer(AiTask.Rationale); // US-117
            _evidenceWriter = models.Writer(AiTask.EvidenceCheck);
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

            // US-059 R3: then the evidence suggestions, in what is left of the batch
            foreach (var suggestion in await _evidenceSuggestions.ListWithoutRationaleAsync(Math.Max(0, take - written), cancellationToken))
            {
                var requirement = await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken);
                var evidence = await _evidence.GetAsync(suggestion.ProjectId, suggestion.EvidenceId, cancellationToken);
                if (requirement is null || evidence is null)
                {
                    suggestion.SetRationale("(The evidence or the requirement no longer exists.)", _evidenceWriter.Name);
                }
                else
                {
                    var content = evidence.IsFile ? await _evidence.GetContentAsync(evidence.Id, cancellationToken) : null;
                    var text = content is not null && evidence.ContentType is not null ? _evidenceText.Read(evidence.ContentType, content) : null;
                    var prompt = EvidenceRationalePrompt.For(evidence.Title, EvidenceService.EvidenceText(evidence, text),
                        requirement.Code, MappingSuggestionService.RequirementText(requirement));
                    suggestion.SetRationale(await _evidenceWriter.WriteAsync(prompt, cancellationToken), _evidenceWriter.Name);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                written++;
            }

            return written;
        }
    }
}
