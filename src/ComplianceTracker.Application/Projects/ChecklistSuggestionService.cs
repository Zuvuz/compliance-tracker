using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Projects
{
    // US-193: storage for the suggestions (implemented in Infrastructure)
    public interface IChecklistSuggestionRepository
    {
        Task<IReadOnlyList<ChecklistSuggestion>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<ChecklistSuggestion> suggestions, CancellationToken cancellationToken = default);
    }

    public sealed record ChecklistSuggestionView(ChecklistSuggestion Suggestion, string Framework);

    // US-193 R2: the best phase for each requirement, or none when it fits no phase well enough. Pure.
    public static class ChecklistSuggestionRules
    {
        public static (LifecyclePhase Phase, double Score)? BestPhase(IReadOnlyList<double> scores, double minimum)
        {
            if (scores.Count != Enum.GetValues<LifecyclePhase>().Length)
            {
                throw new ArgumentException("One score per phase", nameof(scores));
            }

            var best = scores.Select((score, i) => (Phase: (LifecyclePhase)i, Score: score)).MaxBy(s => s.Score);
            return best.Score >= minimum && best.Score > 0 ? best : null;
        }

        // R4: the approved items whose requirement is no longer in the framework's newest version
        public static IReadOnlyList<OrganisationChecklistItem> Gone(IEnumerable<OrganisationChecklistItem> items, Guid frameworkId, IReadOnlySet<string> codes)
            => items.Where(i => i.IsActive && i.FrameworkId == frameworkId && i.Reference.Length > 0 && !codes.Contains(i.Reference)).ToList();
    }

    // US-193: checklist items for frameworks the tracker does not know in advance, suggested by the AI and decided by a person
    public class ChecklistSuggestionService
    {
        private readonly IChecklistSuggestionRepository _suggestions;
        private readonly IOrganisationChecklistRepository _items;
        private readonly IFrameworkRepository _frameworks;
        private readonly IEmbeddingModel _embeddings;
        private readonly IAiLimits _limits;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ChecklistSuggestionService(IChecklistSuggestionRepository suggestions, IOrganisationChecklistRepository items, IFrameworkRepository frameworks,
            IAiModels models, IAiLimits limits, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _suggestions = suggestions;
            _items = items;
            _frameworks = frameworks;
            _embeddings = models.Embedding(AiTask.Embedding);
            _limits = limits;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // The pending suggestions, newest framework first
        public async Task<IReadOnlyList<ChecklistSuggestionView>> PendingAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            var names = (await _frameworks.ListAsync(organization.Id, cancellationToken)).ToDictionary(f => f.Id, f => FrameworkSlugs.Short(f.Name));
            return (await _suggestions.ListAsync(organization.Id, cancellationToken))
                .Where(s => s.IsPending && names.ContainsKey(s.FrameworkId))
                .OrderBy(s => s.Phase).ThenBy(s => s.RequirementCode, StringComparer.Ordinal)
                .Select(s => new ChecklistSuggestionView(s, names[s.FrameworkId]))
                .ToList();
        }

        // R2, R4: for one of the organisation's own frameworks: removes the items whose requirement is gone, and suggests
        // the requirements not suggested before. Throws AiUnavailableException when the AI cannot be reached.
        public async Task<int> SuggestAsync(Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var framework = (await _frameworks.ListAvailableWithRequirementsAsync(organization.Id, [frameworkId], cancellationToken)).FirstOrDefault()
                ?? throw new DomainException("Framework not found");
            if (framework.OwnerOrganizationId != organization.Id)
            {
                throw new DomainException("The built-in frameworks have their checklist items already");
            }

            var version = framework.Versions.Where(v => v.IsActive).OrderByDescending(v => v.Number).FirstOrDefault()
                ?? throw new DomainException("The framework has no active version");
            var codes = version.Requirements.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);

            // R4: an item whose requirement left the framework goes; what was ticked stays in the projects' history
            var items = await _items.ListAsync(organization.Id, cancellationToken);
            foreach (var gone in ChecklistSuggestionRules.Gone(items, framework.Id, codes))
            {
                gone.Remove(Now);
                await _log.LogAsync(ActivityAction.OrganisationChecklistItemRemoved, organization.Id,
                    $"{gone.Phase}: {gone.Text} ({gone.Source} {gone.Reference}), the requirement is no longer in the framework", cancellationToken);
            }

            var known = (await _suggestions.ListAsync(organization.Id, cancellationToken)).Where(s => s.FrameworkId == framework.Id).Select(s => s.RequirementCode)
                .Concat(items.Where(i => i.IsActive && i.FrameworkId == framework.Id).Select(i => i.Reference))
                .ToHashSet(StringComparer.Ordinal);
            var candidates = version.Requirements.Where(r => !known.Contains(r.Code) && !r.IsRecommendation).ToList();
            if (candidates.Count == 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return 0;
            }

            var phases = Enum.GetValues<LifecyclePhase>();
            var vectors = await _embeddings.EmbedAsync([.. phases.Select(PhaseDescriptions.For), .. candidates.Select(Ai.MappingSuggestionService.RequirementText)], cancellationToken);
            var made = new List<ChecklistSuggestion>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var requirement = vectors[phases.Length + i];
                var scores = phases.Select((_, p) => Similarity.Cosine(vectors[p], requirement)).ToList();
                if (ChecklistSuggestionRules.BestPhase(scores, _limits.MinimumScoreForEvidence) is { } best)
                {
                    made.Add(ChecklistSuggestion.Create(organization.Id, framework.Id, candidates[i].Code, best.Phase, candidates[i].Title, best.Score, _embeddings.Name, Now));
                }
            }

            await _suggestions.AddRangeAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.ChecklistItemsSuggested, organization.Id,
                $"{made.Count} checklist items suggested for {FrameworkSlugs.Short(framework.Name)} ({_embeddings.Name})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return made.Count;
        }

        // R2, R3: approved with the text and the phase the person may have changed; becomes an organisation item with the
        // framework and the requirement code as its source
        public async Task ApproveAsync(Guid suggestionId, string? text, LifecyclePhase? phase, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var suggestion = (await _suggestions.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(s => s.Id == suggestionId)
                ?? throw new DomainException("Suggestion not found");
            var framework = await _frameworks.GetAvailableAsync(organization.Id, suggestion.FrameworkId, cancellationToken)
                ?? throw new DomainException("Framework not found");
            var item = OrganisationChecklistItem.Create(organization.Id, phase ?? suggestion.Phase, string.IsNullOrWhiteSpace(text) ? suggestion.Text : text,
                framework.Id, FrameworkSlugs.Short(framework.Name), suggestion.RequirementCode, Now);
            await _items.AddAsync(item, cancellationToken);
            suggestion.Approve(item.Id, user.Id, Now);
            await _log.LogAsync(ActivityAction.OrganisationChecklistItemAdded, organization.Id, $"{item.Phase}: {item.Text} ({item.Source} {item.Reference})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectAsync(Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var suggestion = (await _suggestions.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(s => s.Id == suggestionId)
                ?? throw new DomainException("Suggestion not found");
            suggestion.Reject(user.Id, Now);
            await _log.LogAsync(ActivityAction.ChecklistSuggestionRejected, organization.Id, $"{suggestion.RequirementCode}: {suggestion.Text}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
