using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-136 R2: storage for the AI's suggestions on the organisation's documents (implemented in Infrastructure)
    public interface IDocumentLinkSuggestionRepository
    {
        // Every suggestion of the organisation, decided ones too
        Task<IReadOnlyList<DocumentLinkSuggestion>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<DocumentLinkSuggestion> suggestions, CancellationToken cancellationToken = default);
    }

    // A pending suggestion as the Documents page shows it, e.g. ("NIS2", "Art. 21(2)(b)", "Incident handling", 0.82)
    public record DocumentSuggestionView(DocumentLinkSuggestion Suggestion, string Framework, string Title);

    // US-136 R2: the local AI suggests which requirements an uploaded document proves, the same way as for a project's
    // evidence (US-059), over every framework the organisation uses (NIS2 in the organisation area, the CRA in the
    // projects …). The suggestion belongs to the document, so it is decided once; approving links the document, and it
    // becomes evidence wherever the requirement is followed (OrganisationDocumentService.SyncAsync). Needs ManageFrameworks,
    // like every other change to the documents.
    public class DocumentSuggestionService
    {
        public const string PromptVersion = "document-v1";

        private readonly IOrganisationDocumentRepository _documents;
        private readonly IDocumentLinkSuggestionRepository _suggestions;
        private readonly IFrameworkRepository _frameworks;
        private readonly OrganisationDocumentService _documentService;
        private readonly IEvidenceTextReader _text;
        private readonly IEmbeddingModel _embeddings;
        private readonly IAiLimits _limits;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public DocumentSuggestionService(
            IOrganisationDocumentRepository documents,
            IDocumentLinkSuggestionRepository suggestions,
            IFrameworkRepository frameworks,
            OrganisationDocumentService documentService,
            IEvidenceTextReader text,
            IAiModels models,
            IAiLimits limits,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _documents = documents;
            _suggestions = suggestions;
            _frameworks = frameworks;
            _documentService = documentService;
            _text = text;
            _embeddings = models.Embedding(AiTask.Embedding); // US-117: the same model as for a project's evidence
            _limits = limits;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // After an upload: the best requirements, at most EvidenceService.BatchSize. After a new version the pending ones
        // are replaced, since the text has changed; decided ones are never suggested again. "Show more" (more) gives the
        // next ones. Throws AiUnavailableException when the AI cannot be reached; the document is kept anyway.
        public async Task<int> SuggestAsync(Guid documentId, bool more = false, bool newVersion = false, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var document = await _documents.GetAsync(organization.Id, documentId, cancellationToken)
                ?? throw new DomainException("Document not found");
            if (!document.IsActive)
            {
                throw new DomainException("The document has been removed");
            }

            var existing = (await _suggestions.ListAsync(organization.Id, cancellationToken)).Where(s => s.DocumentId == document.Id).ToList();
            if (newVersion)
            {
                existing.ForEach(s => s.Replace());
            }

            var candidates = (await RequirementsAsync(organization.Id, cancellationToken))
                .Where(c => !document.ActiveLinks.Any(l => l.FrameworkId == c.FrameworkId
                    && string.Equals(l.RequirementCode, c.Requirement.Code, StringComparison.OrdinalIgnoreCase)))
                .Where(c => !existing.Any(s => s.IsFor(c.FrameworkId, c.Requirement.Code) && (more || s.Status != SuggestionStatus.Replaced)))
                .ToList();
            if (candidates.Count == 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return 0;
            }

            var content = await _documents.GetContentAsync(document.Id, document.Sha256, cancellationToken);
            var text = content is null ? null : _text.Read(document.ContentType, content);
            var vectors = await _embeddings.EmbedAsync(
                [DocumentText(document, text), .. candidates.Select(c => MappingSuggestionService.RequirementText(c.Requirement))], cancellationToken);

            var rank = existing.Select(s => s.Rank).DefaultIfEmpty(0).Max() + 1;
            var made = candidates
                .Select((c, i) => (Candidate: c, Score: Similarity.Cosine(vectors[0], vectors[i + 1])))
                .Where(c => c.Score >= _limits.MinimumScoreForEvidence) // US-105 R1: the same limit as for evidence
                .OrderByDescending(c => c.Score)
                .Take(EvidenceService.BatchSize)
                .Select(c => DocumentLinkSuggestion.Create(organization.Id, document.Id, c.Candidate.FrameworkId, c.Candidate.Requirement.Code,
                    c.Score, rank++, _embeddings.Name, PromptVersion, Now, _limits.MinimumScoreForEvidence))
                .ToList();

            if (made.Count > 0)
            {
                await _suggestions.AddRangeAsync(made, cancellationToken);
                await _log.LogAsync(ActivityAction.DocumentLinkSuggested, organization.Id,
                    $"{made.Count} suggestions for {document.Title} ({_embeddings.Name}, {PromptVersion})", cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return made.Count;
        }

        // "Show more": the next requirements; none that was ever suggested or linked
        public async Task<int> ShowMoreAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var made = await SuggestAsync(documentId, more: true, cancellationToken: cancellationToken);
            return made > 0 ? made : throw new DomainException("There are no more requirements to suggest");
        }

        // Approving links the document to the requirement (US-136 R1), so it becomes evidence wherever it is followed
        public async Task ApproveAsync(Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (organization, user, suggestion, document) = await OpenAsync(suggestionId, cancellationToken);
            suggestion.Approve(user.Id, Now, user.Role);
            await _log.LogAsync(ActivityAction.DocumentLinkApproved, organization.Id, $"{document.Title} ↔ {suggestion.RequirementCode}", cancellationToken);

            if (document.ActiveLinks.Any(l => l.FrameworkId == suggestion.FrameworkId
                && string.Equals(l.RequirementCode, suggestion.RequirementCode, StringComparison.OrdinalIgnoreCase)))
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken); // linked by hand meanwhile
                return;
            }

            // Logs the link, makes the projects' evidence match, and saves the approval with it
            await _documentService.LinkAsync(document.Id, suggestion.FrameworkId, suggestion.RequirementCode, cancellationToken);
        }

        // A reason is needed; the pair is never suggested again
        public async Task RejectAsync(Guid suggestionId, RejectionReason? reason, string? comment, CancellationToken cancellationToken = default)
        {
            var (organization, user, suggestion, document) = await OpenAsync(suggestionId, cancellationToken);
            suggestion.Reject(reason, comment, user.Id, Now, user.Role);
            await _log.LogAsync(ActivityAction.DocumentLinkRejected, organization.Id, $"{document.Title} ↔ {suggestion.RequirementCode} ({reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The pending suggestions per document, best first. One that a person has linked by hand, or whose document or
        // framework is gone, is left out: it needs no decision.
        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DocumentSuggestionView>>> PendingAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return new Dictionary<Guid, IReadOnlyList<DocumentSuggestionView>>();
            }

            var pending = (await _suggestions.ListAsync(organization.Id, cancellationToken)).Where(s => s.IsPending).ToList();
            if (pending.Count == 0)
            {
                return new Dictionary<Guid, IReadOnlyList<DocumentSuggestionView>>();
            }

            var documents = (await _documents.ListAsync(organization.Id, cancellationToken: cancellationToken)).Where(d => d.IsActive).ToDictionary(d => d.Id);
            var requirements = await RequirementsAsync(organization.Id, cancellationToken);
            return pending
                .Where(s => documents.TryGetValue(s.DocumentId, out var d)
                    && !d.ActiveLinks.Any(l => l.FrameworkId == s.FrameworkId && string.Equals(l.RequirementCode, s.RequirementCode, StringComparison.OrdinalIgnoreCase)))
                .Select(s => (Suggestion: s, Requirement: requirements.FirstOrDefault(r => s.IsFor(r.FrameworkId, r.Requirement.Code))))
                .Where(x => x.Requirement.Requirement is not null)
                .GroupBy(x => x.Suggestion.DocumentId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<DocumentSuggestionView>)g.OrderByDescending(x => x.Suggestion.Score)
                        .Select(x => new DocumentSuggestionView(x.Suggestion, x.Requirement.Framework, x.Requirement.Requirement.Title))
                        .ToList());
        }

        // "Needs attention": how many suggestions wait for a decision
        public async Task<int> PendingCountAsync(CancellationToken cancellationToken = default)
            => (await PendingAsync(cancellationToken)).Values.Sum(l => l.Count);

        // The document's title and text, as the AI reads it. The title is the first line.
        public static string DocumentText(OrganisationDocument document, string? text)
        {
            var all = string.IsNullOrWhiteSpace(text) ? document.Title : $"{document.Title}\n{text}";
            return all.Length <= 2000 ? all : all[..2000];
        }

        // The requirements of the newest active version of every framework the organisation uses, as for linking by hand
        private async Task<IReadOnlyList<(Guid FrameworkId, string Framework, Requirement Requirement)>> RequirementsAsync(
            Guid organizationId, CancellationToken cancellationToken)
        {
            var available = await _frameworks.ListAsync(organizationId, cancellationToken);
            var frameworks = available.Count == 0 ? [] : await _frameworks.ListAvailableWithRequirementsAsync(organizationId, available.Select(f => f.Id).ToList(), cancellationToken);
            return frameworks
                .Select(f => (Framework: f, Version: f.Versions.Where(v => v.IsActive).OrderByDescending(v => v.Number).FirstOrDefault()))
                .Where(x => x.Version is not null)
                .SelectMany(x => x.Version!.Requirements.Select(r => (x.Framework.Id, x.Framework.ShortName, r)))
                .ToList();
        }

        private async Task<(Organization Organization, User User, DocumentLinkSuggestion Suggestion, OrganisationDocument Document)> OpenAsync(
            Guid suggestionId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var suggestion = (await _suggestions.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(s => s.Id == suggestionId)
                ?? throw new DomainException("The suggestion was not found");
            var document = await _documents.GetAsync(organization.Id, suggestion.DocumentId, cancellationToken);
            if (document is not { IsActive: true })
            {
                throw new DomainException("The document has been removed");
            }

            return (organization, user, suggestion, document);
        }
    }
}
