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
        private readonly IEvidenceStoryRepository _stories; // US-146
        private readonly WorkItems.IWorkItemRepository _workItems; // US-146
        private readonly IReportSnapshotSource _reports; // US-146 R8
        private readonly IEvidenceTextReader _text;
        private readonly IEmbeddingModel _embeddings;
        private readonly IReranker? _reranker; // US-118
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
            IEvidenceStoryRepository stories,
            WorkItems.IWorkItemRepository workItems,
            IReportSnapshotSource reports,
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
            _stories = stories;
            _workItems = workItems;
            _reports = reports;
            _text = text;
            _embeddings = models.Embedding(AiTask.Embedding); // US-117
            _reranker = models.Reranker(); // US-118
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
                throw new DomainException("There are no more requirements or stories to suggest");
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
            var rejectedRequirement = (await RequirementsOfProjectAsync(organizationId, project, cancellationToken)).GetValueOrDefault(suggestion.RequirementId);
            var code = rejectedRequirement?.Code ?? "?";

            suggestion.Reject(reason, comment, user.Id, Now, user.Role);
            if (rejectedRequirement is not null)
            {
                suggestion.JudgedOn(Fingerprint.Pair(Ai.RejectionMemory.Of(evidence), Ai.RejectionMemory.Of(rejectedRequirement))); // US-145 R1
            }
            await _log.LogAsync(ActivityAction.EvidenceLinkRejected, project.Id, $"{code}: {evidence.Title} ({reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // ---------------------------------------------------------- US-146: evidence and user stories

        // R5: evidence shows that a user story or product requirement is done
        public async Task LinkStoryAsync(Guid projectId, Guid evidenceId, Guid workItemId, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, evidenceId, cancellationToken);
            var story = await RequireStoryAsync(project, workItemId, cancellationToken);
            await AddStoryLinkAsync(project, evidence, story, user, LinkSource.Manual, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task UnlinkStoryAsync(Guid projectId, Guid linkId, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var link = (await _stories.ListActiveLinksAsync(project.Id, cancellationToken)).FirstOrDefault(l => l.Id == linkId)
                ?? throw new DomainException("The link was not found");
            var evidence = await RequireEvidenceAsync(project, link.EvidenceId, cancellationToken);
            var story = (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == link.WorkItemId);
            link.Remove(user.Id, Now);
            await _log.LogAsync(ActivityAction.EvidenceUnlinkedFromStory, project.Id, $"{story?.Title ?? "?"}: {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-145 R6: a person takes back the rejection of a requirement suggestion; it can be suggested again at once
        public async Task UndoRejectionAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (organizationId, _, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await RequireSuggestionAsync(project, suggestionId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, suggestion.EvidenceId, cancellationToken);
            suggestion.UndoRejection();
            var code = (await RequirementsOfProjectAsync(organizationId, project, cancellationToken)).GetValueOrDefault(suggestion.RequirementId)?.Code ?? "?";
            await _log.LogAsync(ActivityAction.SuggestionRejectionUndone, project.Id, $"Evidence {code}: {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R6: approving links them (source AiApproved)
        public async Task ApproveStoryAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await RequireStorySuggestionAsync(project, suggestionId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, suggestion.EvidenceId, cancellationToken);
            var story = await RequireStoryAsync(project, suggestion.WorkItemId, cancellationToken);
            suggestion.Approve(user.Id, Now, user.Role);
            await AddStoryLinkAsync(project, evidence, story, user, LinkSource.AiApproved, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectStoryAsync(Guid projectId, Guid suggestionId, RejectionReason? reason, string? comment, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await RequireStorySuggestionAsync(project, suggestionId, cancellationToken);
            var evidence = await RequireEvidenceAsync(project, suggestion.EvidenceId, cancellationToken);
            var story = (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == suggestion.WorkItemId);
            suggestion.Reject(reason, comment, user.Id, Now, user.Role);
            if (story is not null)
            {
                suggestion.JudgedOn(Fingerprint.Pair(Ai.RejectionMemory.Of(evidence), Ai.RejectionMemory.Of(story))); // US-145 R1
            }

            await _log.LogAsync(ActivityAction.EvidenceStorySuggestionRejected, project.Id, $"{story?.Title ?? "?"}: {evidence.Title} ({reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R8: a locked management report (US-050) used as evidence for one requirement, e.g. ISO/IEC 27001 9.3
        public async Task<Evidence> AddFromReportAsync(Guid projectId, Guid snapshotId, Guid requirementId, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var (snapshot, content) = await _reports.LockedAsync(organizationId, snapshotId, cancellationToken)
                ?? throw new DomainException("The locked report was not found");
            if (snapshot.ProjectId != project.Id || !snapshot.Kind.StartsWith("traceability:management:", StringComparison.Ordinal))
            {
                throw new DomainException("Only a locked management report of this project can be used as evidence");
            }

            var requirement = (await RequirementsOfProjectAsync(organizationId, project, cancellationToken)).GetValueOrDefault(requirementId)
                ?? throw new DomainException("The requirement is not in a framework version the project follows");
            if ((await _applicability.ListActiveAsync(project.Id, cancellationToken)).Any(a => a.RequirementId == requirement.Id))
            {
                throw new DomainException($"{requirement.Code} does not apply to the project");
            }

            var file = EvidenceFile.Create(snapshot.FileName, content);
            var evidence = Evidence.ForFile(project.Id, snapshot.Title, file, null, user.Id, user.Role, Now);
            await _evidence.AddAsync(evidence, file.Content, cancellationToken);
            await _evidence.AddLinkAsync(EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, user.Id, user.Role, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceAdded, project.Id, $"{evidence.Title} (locked report, SHA-256 {snapshot.Sha256})", cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceLinked, project.Id, $"{requirement.Code}: {evidence.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return evidence;
        }

        private async Task AddStoryLinkAsync(Project project, Evidence evidence, Domain.WorkItems.WorkItem story, User user, LinkSource source, CancellationToken cancellationToken)
        {
            if ((await _stories.ListActiveLinksAsync(project.Id, cancellationToken)).Any(l => l.EvidenceId == evidence.Id && l.WorkItemId == story.Id))
            {
                return;
            }

            await _stories.AddLinkAsync(EvidenceStoryLink.Create(project.Id, evidence.Id, story.Id, user.Id, Now, source, user.Role), cancellationToken);
            foreach (var pending in (await _stories.ListSuggestionsAsync(project.Id, cancellationToken))
                         .Where(s => s.IsPending && s.EvidenceId == evidence.Id && s.WorkItemId == story.Id))
            {
                pending.Replace();
            }

            await _log.LogAsync(ActivityAction.EvidenceLinkedToStory, project.Id,
                $"{story.Title}: {evidence.Title}{(source == LinkSource.AiApproved ? " (AI suggestion approved)" : "")}", cancellationToken);
        }

        private async Task<Domain.WorkItems.WorkItem> RequireStoryAsync(Project project, Guid workItemId, CancellationToken cancellationToken)
            => (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == workItemId && w.State != Domain.WorkItems.WorkItemState.Removed)
               ?? throw new DomainException("The user story was not found");

        private async Task<EvidenceStorySuggestion> RequireStorySuggestionAsync(Project project, Guid suggestionId, CancellationToken cancellationToken)
            => (await _stories.ListSuggestionsAsync(project.Id, cancellationToken)).FirstOrDefault(s => s.Id == suggestionId)
               ?? throw new DomainException("The suggestion was not found");

        // R6: the stories of the project the evidence may show are done; none linked, rejected or (for "Show more") ever suggested
        private async Task<int> SuggestStoriesAsync(Project project, Evidence evidence, float[] evidenceVector, bool more, CancellationToken cancellationToken)
        {
            var linked = (await _stories.ListActiveLinksAsync(project.Id, cancellationToken)).Where(l => l.EvidenceId == evidence.Id).Select(l => l.WorkItemId);
            var existing = (await _stories.ListSuggestionsAsync(project.Id, cancellationToken)).Where(s => s.EvidenceId == evidence.Id).ToList();
            var stories = (await _workItems.ListAsync(project.Id, cancellationToken)).Where(w => w.State != Domain.WorkItems.WorkItemState.Removed).ToList();
            string Pair(Guid workItemId) => stories.FirstOrDefault(s => s.Id == workItemId) is { } s
                ? Fingerprint.Pair(Ai.RejectionMemory.Of(evidence), Ai.RejectionMemory.Of(s)) : "";
            var excluded = existing.Where(s => s.Status == SuggestionStatus.Approved || s.StillRejects(Pair(s.WorkItemId))
                    || (more && s.Status is SuggestionStatus.Pending or SuggestionStatus.Replaced))
                .Select(s => s.WorkItemId).Concat(linked).ToHashSet();
            var candidates = stories.Where(w => !excluded.Contains(w.Id)).ToList();
            if (candidates.Count == 0)
            {
                return 0;
            }

            var vectors = await _embeddings.EmbedAsync(candidates.Select(Ai.MappingSuggestionService.StoryText).ToList(), cancellationToken);
            var rank = existing.Select(s => s.Rank).DefaultIfEmpty(0).Max() + 1;
            var made = candidates
                .Select((story, i) => (Story: story, Score: Similarity.Cosine(evidenceVector, vectors[i])))
                .Where(c => c.Score >= _limits.MinimumScoreForEvidence)
                .OrderByDescending(c => c.Score)
                .Take(BatchSize)
                .Select(c => EvidenceStorySuggestion.Create(project.Id, evidence.Id, c.Story.Id, c.Score, rank++, _embeddings.Name, Now, _limits.MinimumScoreForEvidence))
                .ToList();
            if (made.Count == 0)
            {
                return 0;
            }

            await _stories.AddSuggestionsAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceStorySuggested, project.Id, $"{made.Count} stories for {evidence.Title} ({_embeddings.Name})", cancellationToken);
            return made.Count;
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
            var byId = requirements.ToDictionary(r => r.Id);
            string Pair(Guid requirementId) => byId.TryGetValue(requirementId, out var r)
                ? Fingerprint.Pair(Ai.RejectionMemory.Of(evidence), Ai.RejectionMemory.Of(r)) : "";

            // US-145 R2: approved, or rejected while neither side changed (all ever suggested for "Show more", but not a
            // rejection that went stale or was taken back)
            var excluded = existing.Where(s => s.Status == SuggestionStatus.Approved || s.StillRejects(Pair(s.RequirementId))
                    || (more && s.Status is SuggestionStatus.Pending or SuggestionStatus.Replaced))
                .Select(s => s.RequirementId)
                .Concat(linked)
                .Concat(notApplicable)
                .ToHashSet();

            var candidates = requirements.Where(r => !excluded.Contains(r.Id)).ToList();
            var text = content is not null && evidence.ContentType is not null ? _text.Read(evidence.ContentType, content) : null;
            var evidenceText = EvidenceText(evidence, text);
            var vectors = await _embeddings.EmbedAsync(
                [evidenceText, .. candidates.Select(Ai.MappingSuggestionService.RequirementText)], cancellationToken);

            // US-146 R6: the stories it may show are done, with the same evidence vector
            var stories = await SuggestStoriesAsync(project, evidence, vectors[0], more, cancellationToken);

            var rank = existing.Select(s => s.Rank).DefaultIfEmpty(0).Max() + 1;
            // US-105 R1 and US-118: the limit applies to the rerank score when there is a reranker
            var best = await Reranking.BestAsync(_reranker, evidenceText,
                candidates.Select((requirement, i) => (Item: requirement, Search: Similarity.Cosine(vectors[0], vectors[i + 1]))),
                Ai.MappingSuggestionService.RequirementText, _limits.MinimumScoreForEvidence, BatchSize, cancellationToken);
            var made = best
                .Select(c => EvidenceLinkSuggestion.Create(project.Id, evidence.Id, c.Item.Id, c.Score, rank++,
                    _embeddings.Name, EvidenceRationalePrompt.Version, Now, _limits.MinimumScoreForEvidence).Reranked(c.SearchScore, _reranker?.Name))
                .ToList();

            // US-145 R4
            var users = (await _member.FindOrganizationAsync(cancellationToken))?.Users ?? [];
            foreach (var suggestion in made)
            {
                if (existing.Where(s => s.RequirementId == suggestion.RequirementId && s.Status == SuggestionStatus.Rejected).OrderByDescending(s => s.DecidedAt).FirstOrDefault() is { } earlier)
                {
                    suggestion.NoteEarlierRejection(Ai.RejectionMemory.Note(earlier.RejectedFingerprint, Pair(suggestion.RequirementId),
                        users.FirstOrDefault(u => u.Id == earlier.DecidedById)?.Name ?? "Unknown", earlier.DecidedAt, earlier.RejectionReason, "evidence"));
                }
            }

            if (made.Count > 0)
            {
                await _suggestions.AddRangeAsync(made, cancellationToken);
                await _log.LogAsync(ActivityAction.EvidenceLinkSuggested, project.Id,
                    $"{made.Count} suggestions for {evidence.Title} ({_embeddings.Name}, {EvidenceRationalePrompt.Version})", cancellationToken);
            }

            if (made.Count + stories == 0)
            {
                return 0; // R3: nothing good enough; the page says so
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _rationales.Wake();
            return made.Count + stories;
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
