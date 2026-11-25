using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Ai
{
    // US-221: what the confirmation page shows: what is linked to what, the argument, and whether the suggestion still waits
    // (R5: if someone else decided it first, who did)
    public sealed record SuggestionConfirmation(
        Guid ProjectId,
        string ProjectName,
        SuggestionKind Kind,
        Guid SuggestionId,
        string Item,
        string Target,
        SuggestionArgument Argument,
        bool IsPending,
        string? DecidedBy,
        SuggestionStatus Status);

    // US-221 R1, R3: the argument of a suggestion that makes a link, for the confirmation page. Risk measures show theirs on
    // the risk page itself, where the approve form also takes evidence.
    public class SuggestionArgumentService
    {
        private readonly CurrentMember _member;
        private readonly IProjectRepository _projects;
        private readonly IMappingSuggestionRepository _mappings;
        private readonly IEvidenceLinkSuggestionRepository _evidenceSuggestions;
        private readonly IEvidenceStoryRepository _stories;
        private readonly IEvidenceRepository _evidence;
        private readonly IWorkItemRepository _workItems;
        private readonly IFrameworkRepository _frameworks;

        public SuggestionArgumentService(CurrentMember member, IProjectRepository projects, IMappingSuggestionRepository mappings,
            IEvidenceLinkSuggestionRepository evidenceSuggestions, IEvidenceStoryRepository stories, IEvidenceRepository evidence, IWorkItemRepository workItems,
            IFrameworkRepository frameworks)
        {
            _member = member;
            _projects = projects;
            _mappings = mappings;
            _evidenceSuggestions = evidenceSuggestions;
            _stories = stories;
            _evidence = evidence;
            _workItems = workItems;
            _frameworks = frameworks;
        }

        // Null when the project or the suggestion is not the member's organisation's
        public async Task<SuggestionConfirmation?> ForAsync(Guid projectId, SuggestionKind kind, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            if (await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            string? NameOf(Guid? id) => id is { } known ? organization.Users.FirstOrDefault(u => u.Id == known)?.Name ?? "a former member" : null;
            async Task<string> StoryAsync(Guid id)
                => (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == id)?.Title ?? "A removed user story";
            async Task<string> EvidenceAsync(Guid id) => (await _evidence.GetAsync(project.Id, id, cancellationToken))?.Title ?? "Removed evidence";

            switch (kind)
            {
                case SuggestionKind.Story:
                {
                    if (await _mappings.GetAsync(project.Id, suggestionId, cancellationToken) is not { } s
                        || await _frameworks.GetRequirementAsync(s.RequirementId, cancellationToken) is not { } requirement)
                    {
                        return null;
                    }

                    var argument = SuggestionArgument.Build(requirement.Code, requirement.Title, s.Score, null, null, null, s.Rationale,
                        [s.EmbeddingModel, s.RankingModel, s.RationaleModel], s.PromptVersion, s.CreatedAt);
                    return new SuggestionConfirmation(project.Id, project.Name, kind, s.Id, await StoryAsync(s.WorkItemId), $"{requirement.Code} {requirement.Title}",
                        argument, s.IsPending, NameOf(s.DecidedById), s.Status);
                }

                case SuggestionKind.Evidence:
                {
                    if ((await _evidenceSuggestions.ListAsync(project.Id, cancellationToken)).FirstOrDefault(x => x.Id == suggestionId) is not { } s
                        || await _frameworks.GetRequirementAsync(s.RequirementId, cancellationToken) is not { } requirement)
                    {
                        return null;
                    }

                    var argument = SuggestionArgument.Build(requirement.Code, requirement.Title, s.Score, s.CheckPassage, s.CheckVerdict, s.CheckMissing, s.Rationale,
                        [s.EmbeddingModel, s.RankingModel, s.RationaleModel, s.CheckModel], s.PromptVersion, s.CreatedAt);
                    return new SuggestionConfirmation(project.Id, project.Name, kind, s.Id, await EvidenceAsync(s.EvidenceId), $"{requirement.Code} {requirement.Title}",
                        argument, s.IsPending, NameOf(s.DecidedById), s.Status);
                }

                case SuggestionKind.EvidenceStory:
                {
                    if ((await _stories.ListSuggestionsAsync(project.Id, cancellationToken)).FirstOrDefault(x => x.Id == suggestionId) is not { } s)
                    {
                        return null;
                    }

                    var story = await StoryAsync(s.WorkItemId);
                    var argument = SuggestionArgument.Build("The user story", story, s.Score, null, null, null, null,
                        [s.EmbeddingModel, s.RankingModel], null, s.CreatedAt);
                    return new SuggestionConfirmation(project.Id, project.Name, kind, s.Id, await EvidenceAsync(s.EvidenceId), story,
                        argument, s.IsPending, NameOf(s.DecidedById), s.Status);
                }

                default:
                    return null;
            }
        }
    }
}
