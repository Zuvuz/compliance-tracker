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
    // Storage for "needs action" marks (implemented in Infrastructure)
    public interface INeedsActionSuggestionRepository
    {
        Task<IReadOnlyList<NeedsActionSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<NeedsActionSuggestion?> GetAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<NeedsActionSuggestion> suggestions, CancellationToken cancellationToken = default);
    }

    // What the AI answered about one requirement (US-155 R2)
    public abstract record NeedsActionAnswer
    {
        private NeedsActionAnswer() { }

        public sealed record NeedsAction(NeedsActionPriority Priority, string Reason) : NeedsActionAnswer;

        public sealed record DoesNotApply(string Reason) : NeedsActionAnswer;
    }

    // US-155 R2: the prompt that asks whether a requirement applies and still needs work. Its version is stored with every mark.
    public static class NeedsActionPrompt
    {
        public const string Version = "needs-action-v1";
        public const string Marker = "Assess what a product still has to do for a regulatory requirement";
        public const string NeedsAction = "NEEDS ACTION";
        public const string NotApplicable = "NOT APPLICABLE:";

        public static string For(string requirementCode, string requirementText, AiProjectContext context)
            => $"""
                {Marker}. Nothing in the project covers it yet.
                Answer with exactly one line, one of:
                "{NeedsAction} HIGH: <one sentence why>", "{NeedsAction} MEDIUM: <…>" or "{NeedsAction} LOW: <…>" when the
                requirement applies to this product; HIGH when the product clearly has the feature or risk it is about.
                "{NotApplicable} <one sentence why>" only when the product clearly cannot be affected by it.
                "UNSURE" when the description does not say enough.
                Name the purpose, scope or classification you base it on. Do not invent facts.

                Requirement {requirementCode}:
                {requirementText}

                {context.PromptText()}

                Answer:
                """;

        // The first line of the answer as a verdict; null when it is unsure, empty or not in the asked shape
        public static NeedsActionAnswer? Read(string? answer)
        {
            var line = (answer ?? "").Trim().Split('\n')[0].Trim().Trim('"');
            if (line.StartsWith(NotApplicable, StringComparison.OrdinalIgnoreCase))
            {
                var why = line[NotApplicable.Length..].Trim();
                return why.Length == 0 ? null : new NeedsActionAnswer.DoesNotApply(why);
            }

            if (!line.StartsWith(NeedsAction, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var rest = line[NeedsAction.Length..].TrimStart();
            var colon = rest.IndexOf(':');
            if (colon < 0)
            {
                return null;
            }

            var reason = rest[(colon + 1)..].Trim();
            NeedsActionPriority? priority = rest[..colon].Trim().ToUpperInvariant() switch
            {
                "HIGH" => NeedsActionPriority.High,
                "MEDIUM" => NeedsActionPriority.Medium,
                "LOW" => NeedsActionPriority.Low,
                _ => null
            };
            return priority is null || reason.Length == 0 ? null : new NeedsActionAnswer.NeedsAction(priority.Value, reason);
        }
    }

    // US-155: "Find what needs action" asks the local chat model, requirement by requirement, about the applicable,
    // not-covered requirements of one framework. "Applies" becomes a "needs action" mark; "does not apply" becomes the
    // US-060 suggestion. A person confirms ("Create story") or dismisses ("Not needed") each mark.
    public class NeedsActionService
    {
        // A small model on a laptop takes seconds per answer, so one press checks this many requirements (as US-060)
        public const int PerRun = 20;

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IRequirementLinkRepository _links;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IApplicabilitySuggestionRepository _applicabilitySuggestions;
        private readonly INeedsActionSuggestionRepository _marks;
        private readonly IRiskRepository _risks;
        private readonly AiContextBuilder _context;
        private readonly IRationaleWriter _writer;
        private readonly ComplianceService _compliance;
        private readonly AskTheAiService _askTheAi;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public NeedsActionService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IRequirementLinkRepository links,
            IRequirementApplicabilityRepository applicability,
            IApplicabilitySuggestionRepository applicabilitySuggestions,
            INeedsActionSuggestionRepository marks,
            IRiskRepository risks,
            AiContextBuilder context,
            IRationaleWriter writer,
            ComplianceService compliance,
            AskTheAiService askTheAi,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _links = links;
            _applicability = applicability;
            _applicabilitySuggestions = applicabilitySuggestions;
            _marks = marks;
            _risks = risks;
            _context = context;
            _writer = writer;
            _compliance = compliance;
            _askTheAi = askTheAi;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R2, R4: the applicable, not-covered requirements of one framework without an open mark or a pending
        // applicability suggestion. Returns how many were marked as needing action and how many as not applying.
        // R8: throws a DomainException with the AI's message when the model is down; nothing is saved then.
        public async Task<(int NeedsAction, int DoesNotApply)> FindAsync(Guid projectId, Guid frameworkId, CancellationToken cancellationToken = default)
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
            var pendingApplicability = (await _applicabilitySuggestions.ListAsync(project.Id, cancellationToken))
                .Where(s => s.IsPending).Select(s => s.RequirementId).ToHashSet();
            var marked = (await _marks.ListAsync(project.Id, cancellationToken)).Where(m => m.IsOpen).Select(m => m.RequirementId).ToHashSet();
            var openRisks = (await _risks.ListAsync(project.Id, cancellationToken)).Where(r => r.Status != RiskStatus.Closed).ToList();
            var withRisk = openRisks.SelectMany(r => r.Measures).Select(m => m.RequirementId).OfType<Guid>().ToHashSet();
            var withHighRisk = openRisks.Where(r => r.Level >= RiskLevel.High)
                .SelectMany(r => r.Measures).Select(m => m.RequirementId).OfType<Guid>().ToHashSet();

            var targets = NeedsActionTargets.Pick(version.Requirements, linked, decided, pendingApplicability, marked, PerRun);

            var marks = new List<NeedsActionSuggestion>();
            var notApplying = new List<ApplicabilitySuggestion>();
            foreach (var requirement in targets)
            {
                var context = await _context.ForAsync(project, requirement, cancellationToken);
                string answer;
                try
                {
                    answer = await _writer.WriteAsync(
                        NeedsActionPrompt.For(requirement.Code, MappingSuggestionService.RequirementText(requirement), context), cancellationToken);
                }
                catch (AiUnavailableException error)
                {
                    throw new DomainException(error.Message);
                }

                switch (NeedsActionPrompt.Read(answer))
                {
                    case NeedsActionAnswer.NeedsAction needs:
                        marks.Add(NeedsActionSuggestion.Create(project.Id, framework.Id, requirement.Id, needs.Reason, needs.Priority,
                            withHighRisk.Contains(requirement.Id), context.Used, _writer.Name, NeedsActionPrompt.Version, Now));
                        break;
                    // US-060 R7: a requirement an open risk names is never suggested as not applying
                    case NeedsActionAnswer.DoesNotApply no when !withRisk.Contains(requirement.Id):
                        notApplying.Add(ApplicabilitySuggestion.Create(project.Id, framework.Id, requirement.Id, no.Reason, context.Used,
                            context.ClassifiedAt, _writer.Name, NeedsActionPrompt.Version, Now));
                        break;
                }
            }

            await _marks.AddRangeAsync(marks, cancellationToken);
            await _applicabilitySuggestions.AddRangeAsync(notApplying, cancellationToken);
            await _log.LogAsync(ActivityAction.NeedsActionSuggested, project.Id,
                $"{marks.Count} need action and {notApplying.Count} do not apply, of {targets.Count} requirements checked ({_writer.Name}, {NeedsActionPrompt.Version})",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return (marks.Count, notApplying.Count);
        }

        // R5: "Create story" — the person agrees, and the AI drafts a story for the requirement (US-106)
        public async Task<AskResult> CreateStoryAsync(Guid projectId, Guid markId, CancellationToken cancellationToken = default)
        {
            var (project, user, mark) = await OpenAsync(projectId, markId, cancellationToken);
            var requirement = await _frameworks.GetRequirementAsync(mark.RequirementId, cancellationToken)
                ?? throw new DomainException("The requirement no longer exists");
            if (mark.IsPending)
            {
                mark.Confirm(user.Id, Now, user.Role);
                await _log.LogAsync(ActivityAction.NeedsActionConfirmed, project.Id, $"{requirement.Code}: {mark.Reason} ({mark.Model})", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return await _askTheAi.AskAboutRequirementAsync(project.Id, requirement.Id, cancellationToken);
        }

        // R6: "Not needed" — the requirement becomes not applicable with the person's reason (and a risk, US-144 R14)
        public async Task DismissAsync(Guid projectId, Guid markId, string? reason, Guid? riskId, CancellationToken cancellationToken = default)
        {
            var (project, user, mark) = await OpenAsync(projectId, markId, cancellationToken);
            var requirement = await _frameworks.GetRequirementAsync(mark.RequirementId, cancellationToken)
                ?? throw new DomainException("The requirement no longer exists");
            mark.Dismiss(reason ?? "", user.Id, Now, user.Role);

            // Checks the reason and the risk and saves the decision, and this mark with it
            await _compliance.MarkNotApplicableAsync(project.Id, requirement.Id, mark.DecisionReason!, cancellationToken, riskId);
            await _log.LogAsync(ActivityAction.NeedsActionDismissed, project.Id, $"{requirement.Code}: {mark.DecisionReason} (AI: {mark.Reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<(Domain.Projects.Project Project, User User, NeedsActionSuggestion Mark)> OpenAsync(
            Guid projectId, Guid markId, CancellationToken cancellationToken)
        {
            // R7: only members who may decide compliance
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var mark = await _marks.GetAsync(project.Id, markId, cancellationToken)
                ?? throw new DomainException("The mark was not found");
            if (!mark.IsOpen)
            {
                throw new DomainException("The mark has already been decided");
            }

            return (project, user, mark);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }

    // US-155 R1: which requirements one press checks. Pure, so the edge cases are tested on their own.
    public static class NeedsActionTargets
    {
        public static IReadOnlyList<Domain.Frameworks.Requirement> Pick(
            IEnumerable<Domain.Frameworks.Requirement> requirements, ISet<Guid> linked, ISet<Guid> notApplicable,
            ISet<Guid> pendingApplicability, ISet<Guid> marked, int limit)
            => requirements
                // a recommendation ("should") does not count in the coverage (US-143 R6)
                .Where(r => !r.IsRecommendation)
                .Where(r => !linked.Contains(r.Id) && !notApplicable.Contains(r.Id) && !pendingApplicability.Contains(r.Id) && !marked.Contains(r.Id))
                .Take(Math.Max(limit, 0))
                .ToList();
    }
}
