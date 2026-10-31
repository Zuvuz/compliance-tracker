using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-005 and US-006: the organisation's frameworks (uploaded PDFs and built-in regulations).
    // Requires sign-in (FallbackPolicy in Program.cs).
    // Thin controller: FrameworkService changes data, the queries read it, and DomainExceptionFilter
    // sends the user back to the page with the error when a rule is broken.
    public class FrameworksController : Controller
    {
        // A little over 20 MB, so a file that is too large reaches the domain and gets a clear message (R5)
        // instead of being cut off by the web server with a technical error
        private const long MaxRequestSize = 25 * 1024 * 1024;

        private readonly FrameworkService _service;
        private readonly FrameworkQueries _frameworks;
        private readonly ProjectQueries _projects;
        private readonly OrganizationQueries _organizations;

        public FrameworksController(
            FrameworkService service,
            FrameworkQueries frameworks,
            ProjectQueries projects,
            OrganizationQueries organizations)
        {
            _service = service;
            _frameworks = frameworks;
            _projects = projects;
            _organizations = organizations;
        }

        // GET /Frameworks: the list of the organisation's frameworks and the "add" form
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            // Users without an organisation must create one first
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(new FrameworksPageViewModel(
                organization,
                await _frameworks.ListAsync(cancellationToken),
                await _projects.ListAsync(cancellationToken)));
        }

        // POST /Frameworks/Add: a new framework with its first PDF (R1)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Add(string? name, IFormFile? file, CancellationToken cancellationToken)
        {
            var content = await ReadPdfAsync(file, cancellationToken); // first, so a missing file gives "Choose a PDF file"
            await _service.AddAsync(name ?? "", file!.FileName, content, cancellationToken);
            return RedirectToAction(nameof(Index)); // Post/Redirect/Get: a reload does not upload the file again
        }

        // POST /Frameworks/UploadVersion: a new version of an existing framework (R3).
        // US-011 R4: the ticked projects move to the new version.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> UploadVersion(Guid frameworkId, IFormFile? file, List<Guid>? projectIds, CancellationToken cancellationToken)
        {
            var content = await ReadPdfAsync(file, cancellationToken);
            await _service.UploadVersionAsync(frameworkId, file!.FileName, content, projectIds, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // GET /Frameworks/Versions/{id}: all versions of one framework
        [HttpGet]
        public async Task<IActionResult> Versions(Guid id, CancellationToken cancellationToken)
        {
            var framework = await _frameworks.GetOwnAsync(id, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            if (framework is null || organization is null)
            {
                return NotFound(); // R7: also when the framework belongs to another organisation
            }

            return View(new FrameworkVersionsViewModel(framework, organization));
        }

        // GET /Frameworks/Document/{versionId}: opens the PDF of one version
        [HttpGet]
        public async Task<IActionResult> Document(Guid id, CancellationToken cancellationToken)
        {
            var document = await _frameworks.GetDocumentAsync(id, cancellationToken);
            if (document is null)
            {
                return NotFound(); // R7: also when the PDF belongs to another organisation
            }

            // ASVS V5.4.1: always sent as a PDF, with our own cleaned file name in Content-Disposition.
            // nosniff stops the browser from guessing another content type.
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(document.Value.Content, "application/pdf", document.Value.Version.FileName);
        }

        // ---------------------------------------------------------- Built-in frameworks (US-006)

        // GET /Frameworks/BuiltIn: every built-in framework, with an On/Off switch for the organisation
        [HttpGet]
        public async Task<IActionResult> BuiltIn(CancellationToken cancellationToken)
        {
            if (await _organizations.MineAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            var (all, switchedOn) = await _frameworks.BuiltInAsync(cancellationToken);
            return View(new BuiltInFrameworksViewModel(all, switchedOn));
        }

        // POST /Frameworks/Enable: switches a built-in framework on (R2)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(BuiltIn))]
        public async Task<IActionResult> Enable(Guid frameworkId, CancellationToken cancellationToken)
        {
            await _service.EnableBuiltInAsync(frameworkId, cancellationToken);
            return RedirectToAction(nameof(BuiltIn));
        }

        // POST /Frameworks/Disable: switches it off again; the history is kept (R4)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(BuiltIn))]
        public async Task<IActionResult> Disable(Guid frameworkId, CancellationToken cancellationToken)
        {
            await _service.DisableBuiltInAsync(frameworkId, cancellationToken);
            return RedirectToAction(nameof(BuiltIn));
        }

        // The uploaded file's bytes. At most 25 MB (MaxRequestSize), so it is fine to read it into memory.
        private static async Task<byte[]> ReadPdfAsync(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                throw new DomainException("Choose a PDF file");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
    }
}
