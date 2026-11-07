using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Ai;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Application.Ai
{
    // Storage for evaluation runs (implemented in Infrastructure)
    public interface IAiEvaluationRepository
    {
        // Newest first
        Task<IReadOnlyList<AiEvaluation>> ListAsync(CancellationToken cancellationToken = default);

        Task AddAsync(AiEvaluation evaluation, CancellationToken cancellationToken = default);
    }

    // US-116 R3: a rationale for a person to grade
    public record RationaleSample(Guid SuggestionId, string RequirementCode, string Story, string Rationale, string Model, string PromptVersion);

    // US-116: measures the AI against the decisions people made. Only the operator (the system admin) runs it, and only
    // the decisions of organisations that share them (US-036) are used; nothing leaves the infrastructure (R1).
    public class AiEvaluationService
    {
        public const int SampleSize = 20;

        private readonly IOrganizationRepository _organizations;
        private readonly Projects.IProjectRepository _projects;
        private readonly IMappingSuggestionRepository _suggestions;
        private readonly IWorkItemRepository _workItems;
        private readonly IFrameworkRepository _frameworks;
        private readonly IAiEvaluationRepository _evaluations;
        private readonly IAiModels _models;
        private readonly IAiLimits _limits;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public AiEvaluationService(
            IOrganizationRepository organizations,
            IMappingSuggestionRepository suggestions,
            IWorkItemRepository workItems,
            IFrameworkRepository frameworks,
            IAiEvaluationRepository evaluations,
            IAiModels models,
            IAiLimits limits,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork,
            TimeProvider clock,
            Projects.IProjectRepository projects)
        {
            _projects = projects;
            _organizations = organizations;
            _suggestions = suggestions;
            _workItems = workItems;
            _frameworks = frameworks;
            _evaluations = evaluations;
            _models = models;
            _limits = limits;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R5: every run, newest first, for the operator only
        public async Task<IReadOnlyList<AiEvaluation>> ListAsync(CancellationToken cancellationToken = default)
        {
            EnsureOperator();
            return await _evaluations.ListAsync(cancellationToken);
        }

        // R3: story matching with the Embedding task's current model. For each approved suggestion: is the approved
        // requirement in the top 1, 3 and 10 of all requirements of its framework version? For each rejected one: does
        // it still score above the limit? R6: suggestions and links are never changed.
        public async Task<AiEvaluation> EvaluateStoryMatchingAsync(CancellationToken cancellationToken = default)
        {
            EnsureOperator();
            var decided = await DecidedAsync(cancellationToken);
            var minimum = _limits.MinimumDecisionsForEvaluation;
            if (decided.Count == 0 || decided.Count < minimum)
            {
                throw new DomainException($"Too few decisions to measure: {decided.Count} of at least {Math.Max(1, minimum)}");
            }

            var embeddings = _models.Embedding(AiTask.Embedding);
            var stories = new Dictionary<Guid, Dictionary<Guid, Domain.WorkItems.WorkItem>>();
            var versions = new Dictionary<Guid, (IReadOnlyList<Requirement> Requirements, IReadOnlyList<float[]> Vectors)>();
            int approved = 0, top1 = 0, top3 = 0, top10 = 0, rejected = 0, rejectedAbove = 0;

            foreach (var (organizationId, suggestion) in decided)
            {
                if (!stories.TryGetValue(suggestion.ProjectId, out var projectStories))
                {
                    projectStories = (await _workItems.ListAsync(suggestion.ProjectId, cancellationToken)).ToDictionary(w => w.Id);
                    stories[suggestion.ProjectId] = projectStories;
                }

                if (!projectStories.TryGetValue(suggestion.WorkItemId, out var story)
                    || await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken) is not { } requirement)
                {
                    continue;
                }

                if (!versions.TryGetValue(requirement.FrameworkVersionId, out var version))
                {
                    var framework = await _frameworks.GetAvailableByVersionWithRequirementsAsync(organizationId, requirement.FrameworkVersionId, cancellationToken);
                    var requirements = framework?.Versions.Single(v => v.Id == requirement.FrameworkVersionId).Requirements.ToList() ?? [requirement];
                    version = (requirements, await EmbedAsync(embeddings, requirements.Select(MappingSuggestionService.RequirementText).ToList(), cancellationToken));
                    versions[requirement.FrameworkVersionId] = version;
                }

                var storyVector = (await EmbedAsync(embeddings, [MappingSuggestionService.StoryText(story)], cancellationToken))[0];
                var scores = version.Requirements.Select((r, i) => (r.Id, Score: Similarity.Cosine(storyVector, version.Vectors[i]))).ToList();
                if (suggestion.Status == SuggestionStatus.Approved)
                {
                    approved++;
                    var rank = scores.OrderByDescending(s => s.Score).Select(s => s.Id).ToList().IndexOf(requirement.Id) + 1;
                    top1 += rank == 1 ? 1 : 0;
                    top3 += rank is >= 1 and <= 3 ? 1 : 0;
                    top10 += rank is >= 1 and <= 10 ? 1 : 0;
                }
                else
                {
                    rejected++;
                    rejectedAbove += scores.First(s => s.Id == requirement.Id).Score >= _limits.MinimumScoreForStories ? 1 : 0;
                }
            }

            var evaluation = AiEvaluation.ForMatching(embeddings.Name, RationalePrompt.Version, approved + rejected,
                decided.Min(d => d.Suggestion.DecidedAt), decided.Max(d => d.Suggestion.DecidedAt),
                Share(top1, approved), Share(top3, approved), Share(top10, approved), Share(rejectedAbove, rejected),
                _currentUser.Identity.Name, Now);
            await _evaluations.AddAsync(evaluation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return evaluation;
        }

        // R3: the newest decided suggestions that have a rationale, for a person to grade
        public async Task<IReadOnlyList<RationaleSample>> RationaleSampleAsync(CancellationToken cancellationToken = default)
        {
            EnsureOperator();
            var samples = new List<RationaleSample>();
            foreach (var (_, suggestion) in (await DecidedAsync(cancellationToken))
                         .Where(d => !string.IsNullOrWhiteSpace(d.Suggestion.Rationale))
                         .OrderByDescending(d => d.Suggestion.DecidedAt)
                         .Take(SampleSize))
            {
                var requirement = await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken);
                var story = (await _workItems.ListAsync(suggestion.ProjectId, cancellationToken)).FirstOrDefault(w => w.Id == suggestion.WorkItemId);
                samples.Add(new RationaleSample(suggestion.Id, requirement?.Code ?? "?", story?.Title ?? "(removed)", suggestion.Rationale!,
                    suggestion.RationaleModel ?? "?", suggestion.PromptVersion));
            }

            return samples;
        }

        // R3: the person's grades (1–5) of the sample; the run keeps the model most of the graded rationales came from
        public async Task<AiEvaluation> GradeRationalesAsync(IReadOnlyDictionary<Guid, int> grades, CancellationToken cancellationToken = default)
        {
            var sample = (await RationaleSampleAsync(cancellationToken)).Where(s => grades.ContainsKey(s.SuggestionId)).ToList();
            var model = sample.GroupBy(s => s.Model).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "?";
            var prompt = sample.GroupBy(s => s.PromptVersion).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "?";

            var evaluation = AiEvaluation.ForRationales(model, prompt, sample.Select(s => grades[s.SuggestionId]).ToList(), _currentUser.Identity.Name, Now);
            await _evaluations.AddAsync(evaluation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return evaluation;
        }

        // R1, US-130 R5: only the decisions the organisations share (AiSharing)
        private async Task<List<(Guid OrganizationId, MappingSuggestion Suggestion)>> DecidedAsync(CancellationToken cancellationToken)
        {
            var decided = new List<(Guid, MappingSuggestion)>();
            foreach (var organization in (await _organizations.ListAllAsync(cancellationToken)).Where(o => o.ShareAiFeedback))
            {
                var excluded = (await _projects.ListAsync(organization.Id, cancellationToken))
                    .Where(p => p.ExcludedFromAiSharing).Select(p => p.Id).ToHashSet();
                decided.AddRange((await _suggestions.ListDecidedAsync(organization.Id, cancellationToken))
                    .Where(s => AiSharing.MayUse(organization, excluded, s))
                    .Select(s => (organization.Id, s)));
            }

            return decided;
        }

        private static async Task<IReadOnlyList<float[]>> EmbedAsync(IEmbeddingModel model, IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            try
            {
                return await model.EmbedAsync(texts, cancellationToken);
            }
            catch (AiUnavailableException error)
            {
                throw new DomainException(error.Message);
            }
        }

        private void EnsureOperator()
        {
            if (!_currentUser.IsSystemAdmin)
            {
                throw new DomainException("Only the system admin can do this");
            }
        }

        private static double Share(int part, int whole) => whole == 0 ? 0 : (double)part / whole;

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
