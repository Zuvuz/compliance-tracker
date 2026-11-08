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

        public DocumentsController(OrganisationDocumentService service)
        {
            _service = service;
        }

        // GET /Documents
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var view = await _service.ViewAsync(cancellationToken);
            return view is null ? RedirectToAction("Index", "Projects") : View(view);
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
            await _service.UploadAsync(kind, title, ownerId, version, name, content, reviewDueOn, cancellationToken);
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

        // GET /Documents/File/{id}: the current file, for any member of the organisation
        public async Task<IActionResult> File(Guid id, CancellationToken cancellationToken)
            => await _service.ContentAsync(id, cancellationToken) is { } file
                ? File(file.Content, file.ContentType, file.FileName)
                : NotFound();

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
