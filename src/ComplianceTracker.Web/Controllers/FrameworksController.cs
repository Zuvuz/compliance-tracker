using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Web.Models;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-005: the organisation's frameworks as PDF files. Requires sign-in (FallbackPolicy in Program.cs).
    // Thin controller: the rules live in FrameworkService and the domain.
    public class FrameworksController : Controller
    {
        // A little over 20 MB, so a file that is too large reaches the domain and gets a clear message (R5)
        // instead of being cut off by the web server with a technical error
        private const long MaxRequestSize = 25 * 1024 * 1024;

        private readonly FrameworkService _service;
        private readonly IFrameworkRepository _frameworks;
        private readonly IOrganizationRepository _organizations;

        public FrameworksController(FrameworkService service, IFrameworkRepository frameworks, IOrganizationRepository organizations)
        {
            _service = service;
            _frameworks = frameworks;
            _organizations = organizations;
        }

        // GET /Frameworks: the list of the organisation's frameworks and the "add" form
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            // Users without an organisation must create one first
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await PageAsync(organization, cancellationToken));
        }

        // POST /Frameworks/Add: a new framework with its first PDF (R1)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        public async Task<IActionResult> Add(string? name, IFormFile? file, CancellationToken cancellationToken)
        {
            return await UploadAsync(async content =>
                await _service.AddAsync(User.ExternalId(), name ?? "", file!.FileName, content, cancellationToken),
                file, cancellationToken);
        }

        // POST /Frameworks/UploadVersion: a new version of an existing framework (R3)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        public async Task<IActionResult> UploadVersion(Guid frameworkId, IFormFile? file, CancellationToken cancellationToken)
        {
            return await UploadAsync(async content =>
                await _service.UploadVersionAsync(User.ExternalId(), frameworkId, file!.FileName, content, cancellationToken),
                file, cancellationToken);
        }

        // GET /Frameworks/Versions/{id}: all versions of one framework
        [HttpGet]
        public async Task<IActionResult> Versions(Guid id, CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            var framework = organization is null ? null : await _frameworks.GetAsync(organization.Id, id, cancellationToken);
            if (framework is null)
            {
                return NotFound(); // R7: also when the framework belongs to another organisation
            }

            return View(new FrameworkVersionsViewModel(framework, organization!));
        }

        // GET /Frameworks/Document/{versionId}: opens the PDF of one version
        [HttpGet]
        public async Task<IActionResult> Document(Guid id, CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            var document = organization is null ? null : await _frameworks.GetDocumentAsync(organization.Id, id, cancellationToken);
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
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await BuiltInPageAsync(organization, cancellationToken));
        }

        // POST /Frameworks/Enable: switches a built-in framework on (R2)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Enable(Guid frameworkId, CancellationToken cancellationToken)
            => SwitchAsync(() => _service.EnableBuiltInAsync(User.ExternalId(), frameworkId, cancellationToken), cancellationToken);

        // POST /Frameworks/Disable: switches it off again; the history is kept (R4)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Disable(Guid frameworkId, CancellationToken cancellationToken)
            => SwitchAsync(() => _service.DisableBuiltInAsync(User.ExternalId(), frameworkId, cancellationToken), cancellationToken);

        // Shared by "on" and "off": call the use case, then show the page again (with the error, if any)
        private async Task<IActionResult> SwitchAsync(Func<Task> change, CancellationToken cancellationToken)
        {
            try
            {
                await change();
                return RedirectToAction(nameof(BuiltIn)); // Post/Redirect/Get
            }
            catch (DomainException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var organization = await MyOrganizationAsync(cancellationToken);
                return View(nameof(BuiltIn), await BuiltInPageAsync(organization!, cancellationToken));
            }
        }

        // All built-in frameworks, and which of them the organisation has switched on
        private async Task<BuiltInFrameworksViewModel> BuiltInPageAsync(Organization organization, CancellationToken cancellationToken)
        {
            var builtIn = await _frameworks.ListBuiltInAsync(cancellationToken);
            var followed = await _frameworks.ListAsync(organization.Id, cancellationToken);
            return new BuiltInFrameworksViewModel(builtIn, followed.Where(f => f.IsBuiltIn).Select(f => f.Id).ToHashSet());
        }

        // Shared by "add" and "new version": read the file, call the use case,
        // then either go back to the list or show the error on the same page
        private async Task<IActionResult> UploadAsync(Func<byte[], Task> upload, IFormFile? file, CancellationToken cancellationToken)
        {
            try
            {
                if (file is null || file.Length == 0)
                {
                    throw new DomainException("Choose a PDF file");
                }

                // The file is at most 25 MB (MaxRequestSize), so it is fine to read it into memory
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                await upload(buffer.ToArray());

                // Post/Redirect/Get: reloading the page afterwards does not upload the file again
                return RedirectToAction(nameof(Index));
            }
            catch (DomainException ex)
            {
                // Show the rule that was broken, e.g. "Only PDF files are allowed"
                ModelState.AddModelError(string.Empty, ex.Message);
                var organization = await MyOrganizationAsync(cancellationToken);
                return View(nameof(Index), await PageAsync(organization!, cancellationToken));
            }
        }

        private async Task<FrameworksPageViewModel> PageAsync(Organization organization, CancellationToken cancellationToken)
            => new(organization, await _frameworks.ListAsync(organization.Id, cancellationToken));

        // The organisation the signed-in user belongs to (null if none yet)
        private Task<Organization?> MyOrganizationAsync(CancellationToken cancellationToken)
            => _organizations.GetByMemberAsync(User.ExternalId(), cancellationToken);
    }
}
