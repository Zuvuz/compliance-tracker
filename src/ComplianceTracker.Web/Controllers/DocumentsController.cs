using ComplianceTracker.Web.Diagnostics;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-136: the organisation's policies, procedures and risk tables, shared with the projects as evidence
    public class DocumentsController : Controller
    {
        // Room for a 10 MB file and the form (EvidenceFile refuses anything larger with a clear message)
        private const long MaxRequestSize = 25 * 1024 * 1024;

        private readonly OrganisationDocumentService _service;
        private readonly DocumentSuggestionService _suggestions;

        public DocumentsController(OrganisationDocumentService service, DocumentSuggestionService suggestions)
        {
            _service = service;
            _suggestions = suggestions;
        }

        // GET /Documents
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var view = await _service.ViewAsync(cancellationToken);
            return view is null
                ? RedirectToAction("Index", "Projects")
                : View(view with { Suggestions = await _suggestions.PendingAsync(cancellationToken), Rejected = await _suggestions.RejectedAsync(cancellationToken) }); // US-136 R2, US-145
        }

        // POST /Documents/Upload: R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Upload(
            OrganisationDocumentKind kind, string? title, Guid ownerId, string? version, IFormFile? file, DateOnly? reviewDueOn,
            CancellationToken cancellationToken)
        {
            var (name, content) = await ReadAsync(file, cancellationToken);
            var document = await _service.UploadAsync(kind, title, ownerId, version, name, content, reviewDueOn, cancellationToken);
            await SuggestAsync(document.Id, newVersion: false, cancellationToken); // US-136 R2
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/NewVersion: R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> NewVersion(Guid documentId, string? version, IFormFile? file, CancellationToken cancellationToken)
        {
            var (name, content) = await ReadAsync(file, cancellationToken);
            await _service.UploadNewVersionAsync(documentId, version, name, content, cancellationToken);
            await SuggestAsync(documentId, newVersion: true, cancellationToken); // US-136 R2: the text has changed
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/Review: R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Review(Guid documentId, DateOnly? nextReviewOn, CancellationToken cancellationToken)
        {
            await _service.MarkReviewedAsync(documentId, nextReviewOn ?? throw new Domain.Common.DomainException("Give the date of the next review"), cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/Link: R1. The requirement is chosen as "frameworkId|code".
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Link(Guid documentId, string? requirement, CancellationToken cancellationToken)
        {
            var parts = (requirement ?? "").Split('|', 2);
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var frameworkId))
            {
                throw new Domain.Common.DomainException("Choose a requirement");
            }

            await _service.LinkAsync(documentId, frameworkId, parts[1], cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/Unlink
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Unlink(Guid documentId, Guid linkId, CancellationToken cancellationToken)
        {
            await _service.UnlinkAsync(documentId, linkId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/Remove: R5, after the confirmation that names the requirements
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Remove(Guid documentId, CancellationToken cancellationToken)
        {
            await _service.RemoveAsync(documentId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/ApproveSuggestion: US-136 R2, links the document to the suggested requirement
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ApproveSuggestion(Guid suggestionId, CancellationToken cancellationToken)
        {
            await _suggestions.ApproveAsync(suggestionId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/RejectSuggestion: US-136 R2, with a reason; the pair is never suggested again
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> RejectSuggestion(
            Guid suggestionId, Domain.Mappings.RejectionReason? reason, string? comment, CancellationToken cancellationToken)
        {
            await _suggestions.RejectAsync(suggestionId, reason, comment, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/UndoRejection: US-145 R6, the pair can be suggested again
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> UndoRejection(Guid suggestionId, CancellationToken cancellationToken)
        {
            await _suggestions.UndoRejectionAsync(suggestionId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Documents/MoreSuggestions: US-136 R2, the next requirements for a document
        [AiRateLimit] // US-097 R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> MoreSuggestions(Guid documentId, CancellationToken cancellationToken)
        {
            try
            {
                await _suggestions.ShowMoreAsync(documentId, cancellationToken);
            }
            catch (Application.Ai.AiUnavailableException e)
            {
                TempData["DocumentAi"] = e.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET /Documents/File/{id}: the current file, for any member of the organisation
        public async Task<IActionResult> File(Guid id, CancellationToken cancellationToken)
            => await _service.ContentAsync(id, cancellationToken) is { } file
                ? File(file.Content, file.ContentType, file.FileName)
                : NotFound();

        // US-136 R2: the AI's suggestions after an upload. Without the AI the document is kept, and the page says why
        // there are no suggestions.
        private async Task SuggestAsync(Guid documentId, bool newVersion, CancellationToken cancellationToken)
        {
            try
            {
                var made = await _suggestions.SuggestAsync(documentId, newVersion: newVersion, cancellationToken: cancellationToken);
                TempData["DocumentAi"] = made == 0
                    ? "The AI found no requirement that the document clearly proves. Link it by hand."
                    : $"The AI suggests {made} requirement{(made == 1 ? "" : "s")} for the document. Approve or reject them below.";
            }
            catch (Application.Ai.AiUnavailableException e)
            {
                TempData["DocumentAi"] = $"The document is saved, but there are no AI suggestions: {e.Message}";
            }
        }

        private static async Task<(string Name, byte[] Content)> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is not { Length: > 0 })
            {
                throw new Domain.Common.DomainException("Choose a file");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            return (file.FileName, buffer.ToArray());
        }
    }
}
