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
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.Ai
{
    // US-106 R3: the prompt for a user story draft; its version is kept with every draft. The answer has four labelled parts.
    public static class StoryDraftPrompt
    {
        public const string Version = "story-draft-v1";
        public const string Marker = "Write a user story";

        public static string For(Requirement requirement, string intendedPurpose)
            => $"""
                {Marker} for a software team so that their product meets one regulatory requirement.
                Use only the texts below. Do not invent facts about the product. Answer in exactly this shape:
                TITLE: <a short title>
                ROLE: <who wants it>
                STORY: As a <role>, I want <what>, so that <why>.
                RULES: <one rule per line>
                EXAMPLES: <one example per line>

                The product's intended purpose:
                {intendedPurpose}

                Requirement {requirement.Code}: {requirement.Title}
                {requirement.Description}
                """;

        // The labelled parts of the answer; an answer without labels is the story itself
        public static (string Title, string Role, string Story, string Rules, string Examples) Parse(string answer, Requirement requirement)
        {
            var parts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string? current = null;
            foreach (var line in (answer ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var colon = line.IndexOf(':');
                var label = colon > 0 ? line[..colon].Trim() : "";
                if (label is "TITLE" or "ROLE" or "STORY" or "RULES" or "EXAMPLES")
                {
                    current = label;
                    parts[current] = [line[(colon + 1)..].Trim()];
                }
                else if (current is not null)
                {
                    parts[current].Add(line.Trim());
                }
            }

            string Part(string label) => parts.TryGetValue(label, out var lines) ? string.Join("\n", lines.Where(l => l.Length > 0)).Trim() : "";
            var story = Part("STORY");
            if (story.Length == 0)
            {
                story = (answer ?? "").Trim();
            }

            var title = Part("TITLE");
            return (title.Length > 0 ? title : $"Meet {requirement.Code} {requirement.Title}", Part("ROLE"), story, Part("RULES"), Part("EXAMPLES"));
        }
    }

    // US-106 R1–R4: what one "Ask the AI" on a requirement gave; AiUnavailable says why there is nothing when the AI was out
    public record AskResult(int Stories, int Evidence, StoryDraft? Draft, string? AiUnavailable)
    {
        public string Summary
            => AiUnavailable ?? (Stories + Evidence > 0
                ? $"The AI suggests {Stories} {(Stories == 1 ? "story" : "stories")} and {Evidence} evidence"
                : Draft is not null ? "Nothing in the project matched, so the AI drafted a user story" : "Nothing in the project matched");
    }

    // US-106: ask the AI about one item at a time. For a requirement (R1–R3): the project's stories and evidence that match
    // it, and when nothing does, a draft user story. For a risk (R12): requirements and stories that could be measures.
    // The AI only suggests; a person decides (R4, R13). Asking again replaces the pending suggestions, never decided ones (R14).
    public class AskTheAiService
    {
        public const int PerKind = 3;
        public const int MeasuresPerRisk = 4;

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementLinkRepository _links;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IMappingSuggestionRepository _storySuggestions;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceLinkSuggestionRepository _evidenceSuggestions;
        private readonly IEvidenceTextReader _text;
        private readonly IStoryDraftRepository _drafts;
        private readonly Risks.IRiskRepository _risks;
        private readonly IRiskMeasureSuggestionRepository _measures;
        private readonly IEmbeddingModel _embeddings;
        private readonly IRationaleWriter _writer;
        private readonly IRationaleQueue _rationales;
        private readonly IAiLimits _limits;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public AskTheAiService(
            IProjectRepository projects, IFrameworkRepository frameworks, IWorkItemRepository workItems, IRequirementLinkRepository links,
            IRequirementApplicabilityRepository applicability, IMappingSuggestionRepository storySuggestions, IEvidenceRepository evidence,
            IEvidenceLinkSuggestionRepository evidenceSuggestions, IEvidenceTextReader text, IStoryDraftRepository drafts,
            Risks.IRiskRepository risks, IRiskMeasureSuggestionRepository measures, IAiModels models, IRationaleQueue rationales,
            IAiLimits limits, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _workItems = workItems;
            _links = links;
            _applicability = applicability;
            _storySuggestions = storySuggestions;
            _evidence = evidence;
            _evidenceSuggestions = evidenceSuggestions;
            _text = text;
            _drafts = drafts;
            _risks = risks;
            _measures = measures;
            _embeddings = models.Embedding(AiTask.Embedding);
            _writer = models.Writer(AiTask.GapDrafts);
            _rationales = rationales;
            _limits = limits;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ---------------------------------------------------------- R1–R3: one requirement

        public async Task<AskResult> AskAboutRequirementAsync(Guid projectId, Guid requirementId, CancellationToken cancellationToken = default)
        {
            var (organizationId, _, project) = await OpenAsync(projectId, Permission.LinkWork, cancellationToken);
            var requirement = (await RequirementsAsync(organizationId, project, cancellationToken)).GetValueOrDefault(requirementId)
                ?? throw new DomainException("The requirement is not in a framework version the project follows");
            if ((await _applicability.ListActiveAsync(project.Id, cancellationToken)).Any(a => a.RequirementId == requirement.Id))
            {
                throw new DomainException($"{requirement.Code} does not apply to the project"); // R7
            }

            try
            {
                var stories = await SuggestStoriesAsync(project, requirement, cancellationToken);
                var evidence = await SuggestEvidenceAsync(project, requirement, cancellationToken);
                StoryDraft? draft = null;
                if (stories + evidence == 0)
                {
                    draft = await DraftAsync(project, requirement, cancellationToken);
                }

                await _log.LogAsync(ActivityAction.RequirementSuggestionsAsked, project.Id,
                    $"{requirement.Code}: {stories} stories, {evidence} evidence{(draft is null ? "" : ", a draft story")} ({_embeddings.Name})", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _rationales.Wake();
                return new AskResult(stories, evidence, draft, null);
            }
            catch (AiUnavailableException error)
            {
                return new AskResult(0, 0, null, error.Message); // R9: nothing else changes
            }
        }

        // R2: the best stories for this requirement; pairs linked or decided are never suggested again
        private async Task<int> SuggestStoriesAsync(Project project, Requirement requirement, CancellationToken cancellationToken)
        {
            var stories = (await _workItems.ListAsync(project.Id, cancellationToken)).Where(w => w.State != WorkItemState.Removed).ToList();
            var existing = (await _storySuggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.RequirementId == requirement.Id).ToList();
            foreach (var pending in existing.Where(s => s.IsPending))
            {
                pending.Replace(); // R14
            }

            // US-145 R2: a rejection blocks only while the story and the requirement are unchanged
            var excluded = existing.Where(s => s.Status == SuggestionStatus.Approved
                    || s.StillRejects(stories.FirstOrDefault(w => w.Id == s.WorkItemId) is { } w ? Domain.Common.Fingerprint.Pair(RejectionMemory.Of(w), RejectionMemory.Of(requirement)) : ""))
                .Select(s => s.WorkItemId)
                .Concat((await _links.ListActiveAsync(project.Id, cancellationToken)).Where(l => l.RequirementId == requirement.Id).Select(l => l.WorkItemId))
                .ToHashSet();
            var candidates = stories.Where(s => !excluded.Contains(s.Id)).ToList();
            if (candidates.Count == 0)
            {
                return 0;
            }

            var vectors = await _embeddings.EmbedAsync(
                [MappingSuggestionService.RequirementText(requirement), .. candidates.Select(MappingSuggestionService.StoryText)], cancellationToken);
            var allSuggestions = await _storySuggestions.ListAsync(project.Id, cancellationToken);
            var made = candidates
                .Select((story, i) => (Story: story, Score: Similarity.Cosine(vectors[0], vectors[i + 1])))
                .Where(c => c.Score > 0 && c.Score >= _limits.MinimumScoreForStories)
                .OrderByDescending(c => c.Score)
                .Take(PerKind)
                .Select(c => MappingSuggestion.Create(project.Id, c.Story.Id, requirement.Id, c.Score,
                    allSuggestions.Where(s => s.WorkItemId == c.Story.Id).Select(s => s.Rank).DefaultIfEmpty(0).Max() + 1,
                    _embeddings.Name, RationalePrompt.Version, Now, _limits.MinimumScoreForStories))
                .ToList();
            await _storySuggestions.AddRangeAsync(made, cancellationToken);
            return made.Count;
        }

        // R2: the best evidence of the project for this requirement (as US-059 does the other way)
        private async Task<int> SuggestEvidenceAsync(Project project, Requirement requirement, CancellationToken cancellationToken)
        {
            var existing = (await _evidenceSuggestions.ListAsync(project.Id, cancellationToken)).Where(s => s.RequirementId == requirement.Id).ToList();
            foreach (var pending in existing.Where(s => s.IsPending))
            {
                pending.Replace(); // R14
            }

            var active = await _evidence.ListActiveAsync(project.Id, cancellationToken);
            var excluded = existing.Where(s => s.Status == SuggestionStatus.Approved
                    || s.StillRejects(active.FirstOrDefault(e => e.Id == s.EvidenceId) is { } e ? Domain.Common.Fingerprint.Pair(RejectionMemory.Of(e), RejectionMemory.Of(requirement)) : ""))
                .Select(s => s.EvidenceId)
                .Concat((await _evidence.ListActiveLinksAsync(project.Id, cancellationToken)).Where(l => l.RequirementId == requirement.Id).Select(l => l.EvidenceId))
                .ToHashSet();
            var candidates = active.Where(e => !excluded.Contains(e.Id)).ToList();
            if (candidates.Count == 0)
            {
                return 0;
            }

            var texts = new List<string> { MappingSuggestionService.RequirementText(requirement) };
            foreach (var item in candidates)
            {
                var content = item.IsFile ? await _evidence.GetContentAsync(item.Id, cancellationToken) : null;
                texts.Add(EvidenceService.EvidenceText(item, content is not null && item.ContentType is not null ? _text.Read(item.ContentType, content) : null));
            }

            var vectors = await _embeddings.EmbedAsync(texts, cancellationToken);
            var all = await _evidenceSuggestions.ListAsync(project.Id, cancellationToken);
            var made = candidates
                .Select((item, i) => (Evidence: item, Score: Similarity.Cosine(vectors[0], vectors[i + 1])))
                .Where(c => c.Score > 0 && c.Score >= _limits.MinimumScoreForEvidence)
                .OrderByDescending(c => c.Score)
                .Take(PerKind)
                .Select(c => EvidenceLinkSuggestion.Create(project.Id, c.Evidence.Id, requirement.Id, c.Score,
                    all.Where(s => s.EvidenceId == c.Evidence.Id).Select(s => s.Rank).DefaultIfEmpty(0).Max() + 1,
                    _embeddings.Name, EvidenceRationalePrompt.Version, Now, _limits.MinimumScoreForEvidence))
                .ToList();
            await _evidenceSuggestions.AddRangeAsync(made, cancellationToken);
            return made.Count;
        }

        // R3: with nothing matching, the chat model drafts a story; an open draft for the requirement is kept instead
        private async Task<StoryDraft> DraftAsync(Project project, Requirement requirement, CancellationToken cancellationToken)
        {
            if ((await _drafts.ListAsync(project.Id, cancellationToken)).FirstOrDefault(d => d.RequirementId == requirement.Id && d.IsOpen) is { } open)
            {
                return open;
            }

            var answer = await _writer.WriteAsync(StoryDraftPrompt.For(requirement, project.IntendedPurpose), cancellationToken);
            var (title, role, story, rules, examples) = StoryDraftPrompt.Parse(answer, requirement);
            var draft = StoryDraft.Write(project.Id, requirement.Id, title, role, story, rules, examples, _writer.Name, StoryDraftPrompt.Version, Now);
            await _drafts.AddAsync(draft, cancellationToken);
            await _log.LogAsync(ActivityAction.StoryDraftWritten, project.Id, $"{requirement.Code}: {draft.Title} ({_writer.Name}, {StoryDraftPrompt.Version})", cancellationToken);
            return draft;
        }

        // ---------------------------------------------------------- R4–R6: drafts

        // R4: accepted with the person's changes. The address opens GitHub's "new issue" page filled in (B12: the tracker does
        // not write to GitHub); null when the project has no GitHub repository.
        public async Task<(StoryDraft Draft, string? NewIssueAddress)> AcceptDraftAsync(
            Guid projectId, Guid draftId, string? title, string? role, string? story, string? rules, string? examples, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenAsync(projectId, Permission.LinkWork, cancellationToken);
            var draft = await DraftOfAsync(project, draftId, cancellationToken);
            draft.Accept(title, role, story, rules, examples, user.Id, Now, user.Role);
            var code = (await RequirementsAsync(organizationId, project, cancellationToken)).GetValueOrDefault(draft.RequirementId)?.Code ?? "?";
            await _log.LogAsync(ActivityAction.StoryDraftAccepted, project.Id, $"{code}: {draft.Title}{(draft.EditedByPerson ? " (edited)" : "")}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var address = project.GitHub is { } github
                ? $"https://github.com/{github.Repository}/issues/new?title={Uri.EscapeDataString(draft.Title)}&labels={Uri.EscapeDataString(github.Label)}&body={Uri.EscapeDataString(draft.IssueBody(code))}"
                : null;
            return (draft, address);
        }

        // R6
        public async Task DiscardDraftAsync(Guid projectId, Guid draftId, string? reason, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenAsync(projectId, Permission.LinkWork, cancellationToken);
            var draft = await DraftOfAsync(project, draftId, cancellationToken);
            draft.Discard(reason, user.Id, Now, user.Role);
            await _log.LogAsync(ActivityAction.StoryDraftDiscarded, project.Id, $"{draft.Title}: {draft.DiscardReason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<StoryDraft> DraftOfAsync(Project project, Guid draftId, CancellationToken cancellationToken)
            => (await _drafts.ListAsync(project.Id, cancellationToken)).FirstOrDefault(d => d.Id == draftId)
               ?? throw new DomainException("The draft was not found");

        // ---------------------------------------------------------- R12: measures for a risk

        public async Task<int> SuggestMeasuresAsync(Guid projectId, Guid riskId, CancellationToken cancellationToken = default)
        {
            var (organizationId, _, project) = await OpenAsync(projectId, Permission.ManageRisks, cancellationToken);
            var risk = await _risks.GetAsync(project.Id, riskId, cancellationToken) ?? throw new DomainException("Risk not found");
            var notApplicable = (await _applicability.ListActiveAsync(project.Id, cancellationToken)).Select(a => a.RequirementId).ToHashSet();
            var requirements = (await RequirementsAsync(organizationId, project, cancellationToken)).Values.Where(r => !notApplicable.Contains(r.Id)).ToList();
            var stories = (await _workItems.ListAsync(project.Id, cancellationToken)).Where(w => w.State != WorkItemState.Removed).ToList();

            var existing = (await _measures.ListAsync(project.Id, cancellationToken)).Where(s => s.RiskId == risk.Id).ToList();
            foreach (var pending in existing.Where(s => s.IsPending))
            {
                pending.Replace(); // R14
            }

            // US-145 R2: a rejection blocks only while the risk and the requirement or story are unchanged
            string Pair(Guid? requirementId, Guid? workItemId)
                => Domain.Common.Fingerprint.Pair(RejectionMemory.Of(risk), requirementId is { } r && requirements.FirstOrDefault(x => x.Id == r) is { } req
                    ? RejectionMemory.Of(req)
                    : stories.FirstOrDefault(x => x.Id == workItemId) is { } st ? RejectionMemory.Of(st) : "");
            bool Excluded(Guid? requirementId, Guid? workItemId)
                => risk.Measures.Any(m => m.RequirementId == requirementId && m.WorkItemId == workItemId)
                   || existing.Any(s => s.IsFor(requirementId, workItemId) && (s.Status == SuggestionStatus.Approved || s.StillRejects(Pair(requirementId, workItemId))));

            var candidates = requirements.Where(r => !Excluded(r.Id, null)).Select(r => (RequirementId: (Guid?)r.Id, WorkItemId: (Guid?)null, Text: MappingSuggestionService.RequirementText(r)))
                .Concat(stories.Where(s => !Excluded(null, s.Id)).Select(s => (RequirementId: (Guid?)null, WorkItemId: (Guid?)s.Id, Text: MappingSuggestionService.StoryText(s))))
                .ToList();
            if (candidates.Count == 0)
            {
                throw new DomainException("There is nothing more to suggest for this risk");
            }

            IReadOnlyList<float[]> vectors;
            try
            {
                vectors = await _embeddings.EmbedAsync([$"{risk.Title}\n{risk.Description}", .. candidates.Select(c => c.Text)], cancellationToken);
            }
            catch (AiUnavailableException error)
            {
                throw new DomainException(error.Message); // R9
            }

            var made = candidates
                .Select((c, i) => (Candidate: c, Score: Similarity.Cosine(vectors[0], vectors[i + 1])))
                .Where(c => c.Score > 0 && c.Score >= _limits.MinimumScoreForStories)
                .OrderByDescending(c => c.Score)
                .Take(MeasuresPerRisk)
                .Select(c => RiskMeasureSuggestion.Create(project.Id, risk.Id, c.Candidate.RequirementId, c.Candidate.WorkItemId, c.Score, _embeddings.Name, Now))
                .ToList();
            await _measures.AddRangeAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.RiskMeasuresSuggested, project.Id, $"{risk.Title}: {made.Count} measures ({_embeddings.Name})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return made.Count;
        }

        // R12: approving adds the measure; never by itself
        public async Task ApproveMeasureAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenAsync(projectId, Permission.ManageRisks, cancellationToken);
            var suggestion = await MeasureOfAsync(project, suggestionId, cancellationToken);
            var risk = await _risks.GetAsync(project.Id, suggestion.RiskId, cancellationToken) ?? throw new DomainException("Risk not found");
            suggestion.Approve(user.Id, Now, user.Role);
            if (!risk.Measures.Any(m => m.RequirementId == suggestion.RequirementId && m.WorkItemId == suggestion.WorkItemId))
            {
                risk.AddMeasure(suggestion.RequirementId, suggestion.WorkItemId);
            }

            await _log.LogAsync(ActivityAction.RiskMeasureSuggestionApproved, project.Id, $"{risk.Title}: {await MeasureNameAsync(project, suggestion, cancellationToken)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectMeasureAsync(Guid projectId, Guid suggestionId, RejectionReason? reason, string? comment, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenAsync(projectId, Permission.ManageRisks, cancellationToken);
            var suggestion = await MeasureOfAsync(project, suggestionId, cancellationToken);
            suggestion.Reject(reason, comment, user.Id, Now, user.Role);

            // US-145 R1: what was judged
            if (await _risks.GetAsync(project.Id, suggestion.RiskId, cancellationToken) is { } judgedRisk)
            {
                var measure = suggestion.RequirementId is { } requirementId
                    ? await _frameworks.GetRequirementAsync(requirementId, cancellationToken) is { } r ? RejectionMemory.Of(r) : ""
                    : (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == suggestion.WorkItemId) is { } w ? RejectionMemory.Of(w) : "";
                suggestion.JudgedOn(Domain.Common.Fingerprint.Pair(RejectionMemory.Of(judgedRisk), measure));
            }
            await _log.LogAsync(ActivityAction.RiskMeasureSuggestionRejected, project.Id, $"{await MeasureNameAsync(project, suggestion, cancellationToken)} ({reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-158: a suggested measure that still waits for a decision, before evidence is added for it
        public async Task<RiskMeasureSuggestion> PendingMeasureAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (_, _, project) = await OpenAsync(projectId, Permission.ManageRisks, cancellationToken);
            var suggestion = await MeasureOfAsync(project, suggestionId, cancellationToken);
            return suggestion.IsPending ? suggestion : throw new DomainException("The suggestion has already been decided");
        }

        private async Task<RiskMeasureSuggestion> MeasureOfAsync(Project project, Guid suggestionId, CancellationToken cancellationToken)
            => (await _measures.ListAsync(project.Id, cancellationToken)).FirstOrDefault(s => s.Id == suggestionId)
               ?? throw new DomainException("The suggestion was not found");

        private async Task<string> MeasureNameAsync(Project project, RiskMeasureSuggestion suggestion, CancellationToken cancellationToken)
            => suggestion.RequirementId is { } requirementId
                ? (await _frameworks.GetRequirementAsync(requirementId, cancellationToken))?.Code ?? "?"
                : (await _workItems.ListAsync(project.Id, cancellationToken)).FirstOrDefault(w => w.Id == suggestion.WorkItemId)?.Title ?? "?";

        // ---------------------------------------------------------- Shared

        private async Task<(Guid OrganizationId, User User, Project Project)> OpenAsync(Guid projectId, Permission permission, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(permission, cancellationToken); // R8
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (organization.Id, user, project);
        }

        private async Task<Dictionary<Guid, Requirement>> RequirementsAsync(Guid organizationId, Project project, CancellationToken cancellationToken)
        {
            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(organizationId, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            var versionIds = project.Frameworks.Select(f => f.FrameworkVersionId).ToHashSet();
            return frameworks.SelectMany(f => f.Versions).Where(v => versionIds.Contains(v.Id)).SelectMany(v => v.Requirements).ToDictionary(r => r.Id);
        }
    }

    // US-106 R5: after a sync, an accepted draft whose issue came in (the same title) is linked to its requirement with the
    // source "AI-drafted", as the person who accepted it
    public class StoryDraftLinker
    {
        private readonly IStoryDraftRepository _drafts;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementLinkRepository _links;
        private readonly IActivityLogger _log;
        private readonly TimeProvider _clock;

        public StoryDraftLinker(IStoryDraftRepository drafts, IWorkItemRepository workItems, IRequirementLinkRepository links, IActivityLogger log, TimeProvider clock)
        {
            _drafts = drafts;
            _workItems = workItems;
            _links = links;
            _log = log;
            _clock = clock;
        }

        // Does not save; the sync does
        public async Task<int> LinkAcceptedAsync(Project project, CancellationToken cancellationToken = default)
        {
            var waiting = (await _drafts.ListAsync(project.Id, cancellationToken)).Where(d => d.Status == StoryDraftStatus.Accepted && d.WorkItemId is null).ToList();
            if (waiting.Count == 0)
            {
                return 0;
            }

            var stories = (await _workItems.ListAsync(project.Id, cancellationToken)).Where(w => w.State != WorkItemState.Removed).ToList();
            var links = await _links.ListActiveAsync(project.Id, cancellationToken);
            var linked = 0;
            foreach (var draft in waiting)
            {
                var story = stories.FirstOrDefault(s => string.Equals(s.Title.Trim(), draft.Title.Trim(), StringComparison.OrdinalIgnoreCase));
                if (story is null)
                {
                    continue;
                }

                draft.LinkedTo(story.Id);
                if (!links.Any(l => l.WorkItemId == story.Id && l.RequirementId == draft.RequirementId))
                {
                    await _links.AddAsync(RequirementLink.Create(project.Id, draft.RequirementId, story.Id, draft.DecidedById!.Value,
                        _clock.GetUtcNow().UtcDateTime, LinkSource.AiDrafted, draft.DecidedRole), cancellationToken);
                }

                await _log.LogSystemAsync(project.OrganizationId, ActivityAction.StoryDraftLinked, project.Id, $"{story.Title} (drafted by the AI)", cancellationToken);
                linked++;
            }

            return linked;
        }
    }
}
