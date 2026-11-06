using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Ai
{
    // Storage for applicability suggestions (implemented in Infrastructure)
    public interface IApplicabilitySuggestionRepository
    {
        Task<IReadOnlyList<ApplicabilitySuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<ApplicabilitySuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<ApplicabilitySuggestion> suggestions, CancellationToken cancellationToken = default);
    }

    // US-060 R6, US-061 R2: the prompt that asks whether a requirement applies. Its version is stored with every suggestion.
    public static class ApplicabilityPrompt
    {
        public const string Version = "applicability-v1";
        public const string Marker = "Decide whether a regulatory requirement applies";
        public const string NotApplicable = "NOT APPLICABLE:";

        public static string For(string requirementCode, string requirementText, AiProjectContext context)
            => $"""
                {Marker} to a product.
                Answer with exactly one line. Write "APPLIES" if it applies or if you are not sure.
                Write "{NotApplicable} <one sentence why>" only when the product clearly cannot be affected by it.
                Name the purpose, scope or classification you base it on. Do not invent facts.

                Requirement {requirementCode}:
                {requirementText}

                {context.PromptText()}

                Answer:
                """;

        // The reason after the marker, or null when the requirement applies or the answer is unclear
        public static string? ReasonFrom(string answer)
        {
            var line = answer.Trim().Split('\n')[0].Trim();
            return line.StartsWith(NotApplicable, StringComparison.OrdinalIgnoreCase) && line.Length > NotApplicable.Length
                ? line[NotApplicable.Length..].Trim()
                : null;
        }
    }

    // US-060 R6–R10, US-061 R2: "Suggest not applicable" asks the local chat model, requirement by requirement, whether
    // a requirement of one framework applies to the project. Only clear "does not apply" answers become suggestions.
    public class ApplicabilitySuggestionService
    {
        // A small model on a laptop takes seconds per answer, so one press checks this many requirements
        public const int PerRun = 20;

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IRequirementLinkRepository _links;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IApplicabilitySuggestionRepository _suggestions;
        private readonly IRiskRepository _risks;
        private readonly AiContextBuilder _context;
        private readonly IRationaleWriter _writer;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ApplicabilitySuggestionService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IRequirementLinkRepository links,
            IRequirementApplicabilityRepository applicability,
            IApplicabilitySuggestionRepository suggestions,
            IRiskRepository risks,
            AiContextBuilder context,
            IRationaleWriter writer,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _links = links;
            _applicability = applicability;
            _suggestions = suggestions;
            _risks = risks;
            _context = context;
            _writer = writer;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // The applicable, not covered requirements of one framework, without a pending suggestion and without an open
        // risk (R7). Returns how many suggestions were made. Throws AiUnavailableException when the model is down (R10).
        public async Task<int> SuggestAsync(Guid projectId, Guid frameworkId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var used = project.Frameworks.FirstOrDefault(f => f.FrameworkId == frameworkId)
                ?? throw new DomainException("The project does not follow this framework");
            var framework = (await _frameworks.ListAvailableWithRequirementsAsync(organization.Id, [frameworkId], cancellationToken)).Single();
            var version = framework.Versions.Single(v => v.Id == used.FrameworkVersionId);

            var linked = (await _links.ListActiveAsync(project.Id, cancellationToken)).Select(l => l.RequirementId).ToHashSet();
            var decided = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).Select(a => a.RequirementId).ToHashSet();
            var pending = (await _suggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.IsPending).Select(s => s.RequirementId).ToHashSet();
            var withRisk = (await _risks.ListAsync(project.Id, cancellationToken))
                .Where(r => r.Status != RiskStatus.Closed)
                .SelectMany(r => r.Measures).Select(m => m.RequirementId).OfType<Guid>().ToHashSet();

            var targets = version.Requirements
                .Where(r => !linked.Contains(r.Id) && !decided.Contains(r.Id) && !pending.Contains(r.Id) && !withRisk.Contains(r.Id))
                .Take(PerRun)
                .ToList();

            var made = new List<ApplicabilitySuggestion>();
            foreach (var requirement in targets)
            {
                var context = await _context.ForAsync(project, requirement, cancellationToken);
                string answer;
                try
                {
                    answer = await _writer.WriteAsync(
                        ApplicabilityPrompt.For(requirement.Code, MappingSuggestionService.RequirementText(requirement), context), cancellationToken);
                }
                catch (AiUnavailableException error)
                {
                    // R10: shown on the page like a broken rule; nothing is saved
                    throw new DomainException(error.Message);
                }

                if (ApplicabilityPrompt.ReasonFrom(answer) is { } reason)
                {
                    made.Add(ApplicabilitySuggestion.Create(project.Id, framework.Id, requirement.Id, reason, context.Used,
                        context.ClassifiedAt, _writer.Name, ApplicabilityPrompt.Version, Now));
                }
            }

            await _suggestions.AddRangeAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.ApplicabilitySuggested, project.Id,
                $"{made.Count} not applicable suggested of {targets.Count} requirements checked ({_writer.Name}, {ApplicabilityPrompt.Version})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return made.Count;
        }

        // R6, R8: the requirement becomes not applicable with the reason the person chose (the AI's, or edited)
        public async Task AcceptAsync(Guid projectId, Guid suggestionId, string? reason, CancellationToken cancellationToken = default)
        {
            var (project, user, suggestion) = await PendingAsync(projectId, suggestionId, cancellationToken);
            var text = string.IsNullOrWhiteSpace(reason) ? suggestion.Reason : reason.Trim();
            var requirement = await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken)
                ?? throw new DomainException("The requirement no longer exists");
            if ((await _applicability.ListActiveAsync(project.Id, cancellationToken)).Any(a => a.RequirementId == requirement.Id))
            {
                throw new DomainException($"{requirement.Code} is already not applicable");
            }

            suggestion.Accept(user.Id, Now, user.Role);
            await _applicability.AddAsync(
                RequirementApplicability.NotApplicable(project.Id, requirement.Id, text, user.Id, Now, user.Role), cancellationToken);
            await _log.LogAsync(ActivityAction.ApplicabilitySuggestionAccepted, project.Id, $"{requirement.Code}: {text}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (project, user, suggestion) = await PendingAsync(projectId, suggestionId, cancellationToken);
            var requirement = await _frameworks.GetRequirementAsync(suggestion.RequirementId, cancellationToken);
            suggestion.Reject(user.Id, Now, user.Role);
            await _log.LogAsync(ActivityAction.ApplicabilitySuggestionRejected, project.Id,
                $"{requirement?.Code ?? "?"}: {suggestion.Reason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<(Domain.Projects.Project Project, User User, ApplicabilitySuggestion Suggestion)> PendingAsync(
            Guid projectId, Guid suggestionId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var suggestion = await _suggestions.GetAsync(project.Id, suggestionId, cancellationToken)
                ?? throw new DomainException("Suggestion not found");
            return (project, user, suggestion);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
