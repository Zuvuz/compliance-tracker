using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;
using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Application.Ai
{
    // US-036 R5–R9: the organisation's consent to use its decisions to improve the AI, and the export.
    public class AiFeedbackService
    {
        private readonly IMappingSuggestionRepository _suggestions;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;

        public AiFeedbackService(
            IMappingSuggestionRepository suggestions,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork)
        {
            _suggestions = suggestions;
            _frameworks = frameworks;
            _workItems = workItems;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        // R5, R6: only an Admin; every change is logged
        public async Task SetSharingAsync(bool share, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            if (organization.SetShareAiFeedback(share, user.Id))
            {
                await _log.LogAsync(ActivityAction.AiFeedbackSharingChanged, organization.Id, share ? "Sharing on" : "Sharing off", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // R7–R9: the decided suggestions as JSON Lines, without names or e-mails. Only for an Admin, only while sharing is on.
        public async Task<string> ExportAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            if (user.Role != UserRole.Admin)
            {
                throw new DomainException("Only an Admin can export AI feedback");
            }

            if (!organization.ShareAiFeedback)
            {
                throw new DomainException("Sharing is off");
            }

            var lines = new StringBuilder();
            var count = 0;
            var stories = new Dictionary<Guid, IReadOnlyList<Domain.WorkItems.WorkItem>>();
            foreach (var suggestion in await _suggestions.ListDecidedAsync(organization.Id, cancellationToken))
            {
                if (!stories.TryGetValue(suggestion.ProjectId, out var projectStories))
                {
                    projectStories = await _workItems.ListAsync(suggestion.ProjectId, cancellationToken);
                    stories[suggestion.ProjectId] = projectStories;
                }

                var story = projectStories.FirstOrDefault(w => w.Id == suggestion.WorkItemId);
                var requirement = await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken);
                if (story is null || requirement is null)
                {
                    continue;
                }

                lines.AppendLine(JsonSerializer.Serialize(new
                {
                    requirement = MappingSuggestionService.RequirementText(requirement),
                    story = MappingSuggestionService.StoryText(story),
                    score = suggestion.Score,
                    rank = suggestion.Rank,
                    rationale = suggestion.Rationale,
                    decision = suggestion.Status.ToString(),
                    reason = suggestion.RejectionReason?.ToString(),
                    comment = suggestion.Comment,
                    embeddingModel = suggestion.EmbeddingModel,
                    rationaleModel = suggestion.RationaleModel,
                    promptVersion = suggestion.PromptVersion,
                    suggestedAt = suggestion.CreatedAt,
                    decidedAt = suggestion.DecidedAt
                }));
                count++;
            }

            await _log.LogAsync(ActivityAction.AiFeedbackExported, organization.Id, $"{count} lines", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return lines.ToString();
        }
    }
}
