using ComplianceTracker.Domain.Users;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Ai
{
    // Storage for AI suggestions (implemented in Infrastructure)
    public interface IMappingSuggestionRepository
    {
        // Every suggestion of the project, in every status (also replaced ones)
        Task<IReadOnlyList<MappingSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<MappingSuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<MappingSuggestion> suggestions, CancellationToken cancellationToken = default);

        // For the background job (no organisation; nothing is shown to a user): pending suggestions without a rationale
        Task<IReadOnlyList<MappingSuggestion>> ListWithoutRationaleAsync(int take, CancellationToken cancellationToken = default);

        // US-036 R7, R8: the organisation's decided suggestions, for the export
        Task<IReadOnlyList<MappingSuggestion>> ListDecidedAsync(Guid organizationId, CancellationToken cancellationToken = default);
    }

    // Wakes the background job that writes rationales (implemented in Web). It also runs on a timer,
    // so a rationale is never lost if the app restarts.
    public interface IRationaleQueue
    {
        void Wake();
    }

    // US-020 and US-036: the local AI suggests which requirements each user story covers; a person decides.
    public class MappingSuggestionService
    {
        // US-036 R1, R2: suggestions per user story per batch
        public const int BatchSize = 4;

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementLinkRepository _links;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IMappingSuggestionRepository _suggestions;
        private readonly IEmbeddingModel _embeddings;
        private readonly IRationaleQueue _rationales;
        private readonly IWorkItemNotifier _notifier;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public MappingSuggestionService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IWorkItemRepository workItems,
            IRequirementLinkRepository links,
            IRequirementApplicabilityRepository applicability,
            IMappingSuggestionRepository suggestions,
            IEmbeddingModel embeddings,
            IRationaleQueue rationales,
            IWorkItemNotifier notifier,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _workItems = workItems;
            _links = links;
            _applicability = applicability;
            _suggestions = suggestions;
            _embeddings = embeddings;
            _rationales = rationales;
            _notifier = notifier;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // US-020 R1, R8: suggestions for every user story of the project. Pending ones from an earlier run are replaced.
        // Pairs that are linked, approved or rejected are never suggested again.
        public async Task<int> SuggestAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var context = await LoadAsync(projectId, cancellationToken);
            foreach (var pending in context.Existing.Where(s => s.IsPending))
            {
                pending.Replace();
            }

            var excluded = context.Existing.Where(s => s.Status is SuggestionStatus.Approved or SuggestionStatus.Rejected)
                .Select(s => (s.WorkItemId, s.RequirementId))
                .Concat(context.Linked)
                .ToHashSet();

            var made = await SuggestForAsync(context, context.Stories, excluded, startRank: _ => 1, cancellationToken);
            await _log.LogAsync(ActivityAction.MappingsSuggested, context.Project.Id,
                $"{made} suggestions for {context.Stories.Count} user stories ({_embeddings.Name}, {RationalePrompt.Version})", cancellationToken);
            await SaveAndNotifyAsync(context.Project.Id, cancellationToken);
            return made;
        }

        // US-036 R2: the next suggestions for one user story; none that was ever suggested, linked or decided
        public async Task<int> ShowMoreAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken = default)
        {
            var context = await LoadAsync(projectId, cancellationToken);
            var story = context.Stories.FirstOrDefault(s => s.Id == workItemId)
                ?? throw new DomainException("User story not found");

            var excluded = context.Existing.Select(s => (s.WorkItemId, s.RequirementId)).Concat(context.Linked).ToHashSet();
            var nextRank = context.Existing.Where(s => s.WorkItemId == workItemId).Select(s => s.Rank).DefaultIfEmpty(0).Max() + 1;

            var made = await SuggestForAsync(context, [story], excluded, startRank: _ => nextRank, cancellationToken);
            if (made == 0)
            {
                throw new DomainException("There are no more requirements to suggest");
            }

            await SaveAndNotifyAsync(context.Project.Id, cancellationToken);
            return made;
        }

        // US-020 R6, R9: the suggestion becomes a link with the source "AI approved"
        public async Task ApproveAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (project, userId, suggestion) = await SuggestionAsync(projectId, suggestionId, cancellationToken);
            var role = (await _member.RequireAsync(cancellationToken)).User.Role;
            suggestion.Approve(userId, Now, role);

            var active = await _links.ListActiveAsync(project.Id, cancellationToken);
            if (!active.Any(l => l.RequirementId == suggestion.RequirementId && l.WorkItemId == suggestion.WorkItemId))
            {
                await _links.AddAsync(RequirementLink.Create(
                    project.Id, suggestion.RequirementId, suggestion.WorkItemId, userId, Now, LinkSource.AiApproved, role), cancellationToken);
            }

            await _log.LogAsync(ActivityAction.MappingApproved, project.Id, await PairAsync(project, suggestion, cancellationToken), cancellationToken);
            await SaveAndNotifyAsync(project.Id, cancellationToken);
        }

        // US-020 R7, R9: a reason from the fixed list; "Other" needs a comment
        public async Task RejectAsync(Guid projectId, Guid suggestionId, RejectionReason? reason, string? comment, CancellationToken cancellationToken = default)
        {
            var (project, userId, suggestion) = await SuggestionAsync(projectId, suggestionId, cancellationToken);
            suggestion.Reject(reason, comment, userId, Now, (await _member.RequireAsync(cancellationToken)).User.Role);

            await _log.LogAsync(ActivityAction.MappingRejected, project.Id,
                $"{await PairAsync(project, suggestion, cancellationToken)}: {reason}", cancellationToken);
            await SaveAndNotifyAsync(project.Id, cancellationToken);
        }

        // ---------------------------------------------------------- Helpers

        private sealed record Context(
            Project Project,
            IReadOnlyList<WorkItem> Stories,
            IReadOnlyList<Requirement> Requirements,
            IReadOnlyList<MappingSuggestion> Existing,
            IReadOnlyList<(Guid WorkItemId, Guid RequirementId)> Linked);

        // The project's stories (not removed) and the applicable requirements of the versions it follows (US-043)
        private async Task<Context> LoadAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");

            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var stories = (await _workItems.ListAsync(project.Id, cancellationToken))
                .Where(w => w.State != WorkItemState.Removed)
                .ToList();

            var versionIds = project.Frameworks.Select(f => f.FrameworkVersionId).ToHashSet();
            var notApplicable = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).Select(a => a.RequirementId).ToHashSet();
            var requirements = (await _frameworks.ListAvailableWithRequirementsAsync(
                    organization.Id, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken))
                .SelectMany(f => f.Versions)
                .Where(v => versionIds.Contains(v.Id))
                .SelectMany(v => v.Requirements)
                .Where(r => !notApplicable.Contains(r.Id))
                .ToList();

            if (stories.Count == 0 || requirements.Count == 0)
            {
                throw new DomainException("The project needs user stories and frameworks with requirements first");
            }

            var linked = (await _links.ListActiveAsync(project.Id, cancellationToken)).Select(l => (l.WorkItemId, l.RequirementId)).ToList();
            return new Context(project, stories, requirements, await _suggestions.ListAsync(project.Id, cancellationToken), linked);
        }

        // Step 1 of 2: the embedding model scores every requirement against each story; the best BatchSize become
        // suggestions. Step 2, the rationale, is written by the background job (RationaleService).
        private async Task<int> SuggestForAsync(
            Context context, IReadOnlyList<WorkItem> stories, HashSet<(Guid, Guid)> excluded,
            Func<WorkItem, int> startRank, CancellationToken cancellationToken)
        {
            IReadOnlyList<float[]> vectors;
            try
            {
                vectors = await _embeddings.EmbedAsync(
                    stories.Select(StoryText).Concat(context.Requirements.Select(RequirementText)).ToList(), cancellationToken);
            }
            catch (AiUnavailableException error)
            {
                // Shown on the page like a broken rule; nothing is saved
                throw new DomainException(error.Message);
            }
            var storyVectors = vectors.Take(stories.Count).ToList();
            var requirementVectors = vectors.Skip(stories.Count).ToList();

            var now = Now;
            var made = new List<MappingSuggestion>();
            for (var s = 0; s < stories.Count; s++)
            {
                var story = stories[s];
                var best = context.Requirements
                    .Select((requirement, r) => (Requirement: requirement, Score: Similarity.Cosine(storyVectors[s], requirementVectors[r])))
                    .Where(c => !excluded.Contains((story.Id, c.Requirement.Id)))
                    .OrderByDescending(c => c.Score)
                    .Take(BatchSize)
                    .ToList();

                var rank = startRank(story);
                foreach (var (requirement, score) in best)
                {
                    made.Add(MappingSuggestion.Create(context.Project.Id, story.Id, requirement.Id, score, rank++,
                        _embeddings.Name, RationalePrompt.Version, now));
                }
            }

            await _suggestions.AddRangeAsync(made, cancellationToken);
            return made.Count;
        }

        // What the AI reads: the story's title, rules and examples, and the requirement's code, title and text (US-020 R3)
        public static string StoryText(WorkItem story)
            => Cut($"{story.Title}\n{story.Rules}\n{story.Examples}", 2000);

        public static string RequirementText(Requirement requirement)
            => Cut($"{requirement.Code} {requirement.Title}\n{requirement.Description}", 2000);

        private async Task<(Project Project, Guid UserId, MappingSuggestion Suggestion)> SuggestionAsync(
            Guid projectId, Guid suggestionId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            var suggestion = await _suggestions.GetAsync(project.Id, suggestionId, cancellationToken)
                ?? throw new DomainException("Suggestion not found");
            return (project, user.Id, suggestion);
        }

        private async Task<string> PairAsync(Project project, MappingSuggestion suggestion, CancellationToken cancellationToken)
        {
            var story = (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == suggestion.WorkItemId);
            var requirement = await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken);
            return $"{requirement?.Code ?? "?"} ↔ {story?.Title ?? "?"}";
        }

        private async Task SaveAndNotifyAsync(Guid projectId, CancellationToken cancellationToken)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _rationales.Wake();
            await _notifier.WorkItemsChangedAsync(projectId, cancellationToken);
        }

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }

    public static class Similarity
    {
        // 1 = same direction, 0 = unrelated (negative values become 0)
        public static double Cosine(float[] a, float[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }

            return na == 0 || nb == 0 ? 0 : Math.Max(0, dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
        }
    }
}
