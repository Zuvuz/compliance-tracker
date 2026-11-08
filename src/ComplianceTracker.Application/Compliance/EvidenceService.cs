using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Compliance
{
    // Storage for the AI's evidence suggestions (US-059, implemented in Infrastructure)
    public interface IEvidenceLinkSuggestionRepository
    {
        // Every suggestion of the project, in every status
        Task<IReadOnlyList<EvidenceLinkSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<EvidenceLinkSuggestion> suggestions, CancellationToken cancellationToken = default);

        // For the background job: pending suggestions without a rationale, best ranked first
        Task<IReadOnlyList<EvidenceLinkSuggestion>> ListWithoutRationaleAsync(int take, CancellationToken cancellationToken = default);
    }

    // US-059 R3: the text the AI reads from an evidence (PDF, TXT, CSV, Markdown). Null for images and links,
    // which are matched by their title only.
    public interface IEvidenceTextReader
    {
        string? Read(string contentType, byte[] content);
    }

    // The prompt for an evidence rationale; its version is stored with every suggestion (US-036 R3)
    public static class EvidenceRationalePrompt
    {
        public const string Version = "evidence-v1";

        public static string For(string evidenceTitle, string evidenceText, string requirementCode, string requirementText)
            => $"""
                You help a software team find which regulatory requirements a piece of evidence proves.
                In one or two short sentences, explain how the evidence shows that the requirement is met,
                or say clearly that it does not. Use only the texts below. Do not invent facts.

                Requirement {requirementCode}:
                {requirementText}

                Evidence "{evidenceTitle}":
                {evidenceText}

                Rationale:
                """;
    }

    // US-059: the project's Evidence tab. Evidence is added once (R1), linked to many requirements (R2), and the local
    // AI suggests which requirements it proves (R3); a person approves or rejects each suggestion (R4).
    public class EvidenceService
    {
        // R3: suggestions per batch, as US-036
        public const int BatchSize = 4;

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceLinkSuggestionRepository _suggestions;
        private readonly IEvidenceTextReader _text;
        private readonly IEmbeddingModel _embeddings;
        private readonly IRationaleQueue _rationales;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly IAiLimits _limits;

        public EvidenceService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IRequirementApplicabilityRepository applicability,
            IEvidenceRepository evidence,
            IEvidenceLinkSuggestionRepository suggestions,
            IEvidenceTextReader text,
            IAiModels models,
            IRationaleQueue rationales,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock,
            IAiLimits limits)
        {
            _limits = limits;
            _projects = projects;
            _frameworks = frameworks;
            _applicability = applicability;
            _evidence = evidence;
            _suggestions = suggestions;
            _text = text;
            _embeddings = models.Embedding(AiTask.Embedding); // US-117
            _rationales = rationales;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1: a link or a file, once. R3: then the AI suggests requirements; without the AI the evidence is still added
        // (R8) and the result says why there are no suggestions.
        public async Task<EvidenceAdded> AddAsync(
            Guid projectId, string title, string? url, string? fileName, byte[]? content, DateOnly? validUntil,
            CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            Evidence evidence;
            byte[]? stored = null;
            if (content is { Length: > 0 })
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    throw new DomainException("Give a link or a file, not both");
                }

                var file = EvidenceFile.Create(fileName ?? "", content);
                evidence = Evidence.ForFile(project.Id, title, file, validUntil, user.Id, user.Role, Now);
                stored = file.Content;
            }
            else
            {
                evidence = Evidence.ForLink(project.Id, title, url ?? "", validUntil, user.Id, user.Role, Now);
            }

            await _evidence.AddAsync(evidence, stored, cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceAdded, project.Id, evidence.Title, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var made = await SuggestAsync(project, evidence, stored, more: false, cancellationToken);
                return new EvidenceAdded(evidence, made, null);
            }
            catch (AiUnavailableException e)
            {
                return new EvidenceAdded(evidence, 0, e.Message);
            }
        }

        // R2: links the evidence to one or more requirements; pairs that are linked already are skipped
        public async Task LinkAsync(Guid projectId, Guid evidenceId, IReadOnlyCollection<Guid> requirementIds, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, evidenceId, cancellationToken);
            if (requirementIds.Count == 0)
            {
                throw new DomainException("Choose at least one requirement");
            }

            var requirements = await RequirementsOfProjectAsync(organizationId, project, cancellationToken);
            var notApplicable = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).Select(a => a.RequirementId).ToHashSet();
            var links = await _evidence.ListActiveLinksAsync(project.Id, cancellationToken);
            var pending = (await _suggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.IsPending && s.EvidenceId == evidence.Id).ToList();

            foreach (var requirementId in requirementIds.Distinct())
            {
                var requirement = requirements.GetValueOrDefault(requirementId)
                    ?? throw new DomainException("The requirement is not in a framework version the project follows");
                if (notApplicable.Contains(requirement.Id))
                {
                    throw new DomainException($"{requirement.Code} does not apply to the project");
                }

                if (links.Any(l => l.EvidenceId == evidence.Id && l.RequirementId == requirement.Id))
                {
                    continue;
                }

                await _evidence.AddLinkAsync(EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, user.Id, user.Role, Now), cancellationToken);
                foreach (var suggestion in pending.Where(s => s.RequirementId == requirement.Id))
                {
                    suggestion.Replace(); // linked by hand: no decision needed
                }

                await _log.LogAsync(ActivityAction.EvidenceLinked, project.Id, $"{requirement.Code}: {evidence.Title}", cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R7: the link is kept as history
        public async Task UnlinkAsync(Guid projectId, Guid linkId, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var link = (await _evidence.ListActiveLinksAsync(project.Id, cancellationToken)).FirstOrDefault(l => l.Id == linkId)
                ?? throw new DomainException("The link was not found");
            var evidence = await RequireEvidenceAsync(project, link.EvidenceId, cancellationToken);
            var requirements = await RequirementsOfProjectAsync(organizationId, project, cancellationToken);

            link.Remove(user.Id, Now);
            await _log.LogAsync(ActivityAction.EvidenceUnlinked, project.Id,
                $"{requirements.GetValueOrDefault(link.RequirementId)?.Code ?? "?"}: {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R7: off every requirement at once; the evidence and its links are kept as history
        public async Task RemoveAsync(Guid projectId, Guid evidenceId, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, evidenceId, cancellationToken);
            if (evidence.IsFromOrganisation)
            {
                throw new DomainException("This evidence comes from the organisation's documents; change it on the Documents page");
            }

            evidence.Remove(user.Id, Now);
            foreach (var suggestion in (await _suggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.EvidenceId == evidence.Id))
            {
                suggestion.Replace();
            }

            await _log.LogAsync(ActivityAction.EvidenceRemoved, project.Id, evidence.Title, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R3 ("Show more", US-036 R2): the next requirements; none that was ever suggested, linked or decided
        public async Task<int> ShowMoreAsync(Guid projectId, Guid evidenceId, CancellationToken cancellationToken = default)
        {
            var (_, _, project) = await OpenProjectAsync(projectId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, evidenceId, cancellationToken);
            var content = evidence.IsFile ? await _evidence.GetContentAsync(evidence.Id, cancellationToken) : null;

            var made = await SuggestAsync(project, evidence, content, more: true, cancellationToken);
            if (made == 0)
            {
                throw new DomainException("There are no more requirements to suggest");
            }

            return made;
        }

        // R4: approving creates the link (source AiApproved)
        public async Task ApproveAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await RequireSuggestionAsync(project, suggestionId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, suggestion.EvidenceId, cancellationToken);
            var requirement = (await RequirementsOfProjectAsync(organizationId, project, cancellationToken)).GetValueOrDefault(suggestion.RequirementId)
                ?? throw new DomainException("The requirement is not in a framework version the project follows");

            suggestion.Approve(user.Id, Now, user.Role);
            if (!(await _evidence.ListActiveLinksAsync(project.Id, cancellationToken)).Any(l => l.EvidenceId == evidence.Id && l.RequirementId == requirement.Id))
            {
                await _evidence.AddLinkAsync(
                    EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, user.Id, user.Role, Now, LinkSource.AiApproved), cancellationToken);
            }

            await _log.LogAsync(ActivityAction.EvidenceLinkApproved, project.Id, $"{requirement.Code}: {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R4: a reason is needed; the pair is never suggested again
        public async Task RejectAsync(Guid projectId, Guid suggestionId, RejectionReason? reason, string? comment, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await RequireSuggestionAsync(project, suggestionId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, suggestion.EvidenceId, cancellationToken);
            var code = (await RequirementsOfProjectAsync(organizationId, project, cancellationToken)).GetValueOrDefault(suggestion.RequirementId)?.Code ?? "?";

            suggestion.Reject(reason, comment, user.Id, Now, user.Role);
            await _log.LogAsync(ActivityAction.EvidenceLinkRejected, project.Id, $"{code}: {evidence.Title} ({reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The evidence's title and text, as the AI reads it. The title is the first line.
        public static string EvidenceText(Evidence evidence, string? text)
        {
            var all = string.IsNullOrWhiteSpace(text) ? evidence.Title : $"{evidence.Title}\n{text}";
            return all.Length <= 2000 ? all : all[..2000];
        }

        // R3, R5: the best requirements of the versions the project follows, except those that do not apply, are linked,
        // or were already suggested (all of them for "Show more"; the decided ones otherwise)
        private async Task<int> SuggestAsync(Project project, Evidence evidence, byte[]? content, bool more, CancellationToken cancellationToken)
        {
            var requirements = (await RequirementsOfProjectAsync(project.OrganizationId, project, cancellationToken)).Values.ToList();
            var notApplicable = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).Select(a => a.RequirementId).ToHashSet();
            var linked = (await _evidence.ListActiveLinksAsync(project.Id, cancellationToken))
                .Where(l => l.EvidenceId == evidence.Id).Select(l => l.RequirementId);
            var existing = (await _suggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.EvidenceId == evidence.Id).ToList();
            var excluded = existing.Where(s => more || s.Status is SuggestionStatus.Approved or SuggestionStatus.Rejected)
                .Select(s => s.RequirementId)
                .Concat(linked)
                .Concat(notApplicable)
                .ToHashSet();

            var candidates = requirements.Where(r => !excluded.Contains(r.Id)).ToList();
            if (candidates.Count == 0)
            {
                return 0;
            }

            var text = content is not null && evidence.ContentType is not null ? _text.Read(evidence.ContentType, content) : null;
            var vectors = await _embeddings.EmbedAsync(
                [EvidenceText(evidence, text), .. candidates.Select(Ai.MappingSuggestionService.RequirementText)], cancellationToken);

            var rank = existing.Select(s => s.Rank).DefaultIfEmpty(0).Max() + 1;
            var made = candidates
                .Select((requirement, i) => (Requirement: requirement, Score: Similarity.Cosine(vectors[0], vectors[i + 1])))
                .Where(c => c.Score >= _limits.MinimumScoreForEvidence) // US-105 R1
                .OrderByDescending(c => c.Score)
                .Take(BatchSize)
                .Select(c => EvidenceLinkSuggestion.Create(project.Id, evidence.Id, c.Requirement.Id, c.Score, rank++,
                    _embeddings.Name, EvidenceRationalePrompt.Version, Now, _limits.MinimumScoreForEvidence))
                .ToList();

            if (made.Count == 0)
            {
                return 0; // R3: nothing good enough; the page says so
            }

            await _suggestions.AddRangeAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceLinkSuggested, project.Id,
                $"{made.Count} suggestions for {evidence.Title} ({_embeddings.Name}, {EvidenceRationalePrompt.Version})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _rationales.Wake();
            return made.Count;
        }

        private async Task<(Guid OrganizationId, User User, Project Project)> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (organization.Id, user, project);
        }

        private async Task<Evidence> RequireEvidenceAsync(Project project, Guid evidenceId, CancellationToken cancellationToken)
            => await _evidence.GetAsync(project.Id, evidenceId, cancellationToken) ?? throw new DomainException("Evidence not found");

        private async Task<EvidenceLinkSuggestion> RequireSuggestionAsync(Project project, Guid suggestionId, CancellationToken cancellationToken)
            => (await _suggestions.ListAsync(project.Id, cancellationToken)).FirstOrDefault(s => s.Id == suggestionId)
               ?? throw new DomainException("The suggestion was not found");

        // The requirements of the framework versions the project follows now
        private async Task<Dictionary<Guid, Requirement>> RequirementsOfProjectAsync(Guid organizationId, Project project, CancellationToken cancellationToken)
        {
            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(
                organizationId, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            var versionIds = project.Frameworks.Select(f => f.FrameworkVersionId).ToHashSet();
            return frameworks.SelectMany(f => f.Versions).Where(v => versionIds.Contains(v.Id)).SelectMany(v => v.Requirements)
                .ToDictionary(r => r.Id);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }

    // The new evidence, how many suggestions the AI made, and why there are none when the AI could not be reached
    public record EvidenceAdded(Evidence Evidence, int Suggestions, string? AiUnavailable);
}
