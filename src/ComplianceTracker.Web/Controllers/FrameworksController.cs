using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Web.Models;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-005: organisationens frameworks som PDF. Kræver login (FallbackPolicy).
    public class FrameworksController : Controller
    {
        // Lidt over 20 MB, så en for stor fil når frem til domænet og får en forståelig fejl (R5)
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

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            return View(await PageAsync(organization, cancellationToken));
        }

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

        [HttpGet]
        public async Task<IActionResult> Versions(Guid id, CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            var framework = organization is null ? null : await _frameworks.GetAsync(organization.Id, id, cancellationToken);
            if (framework is null)
            {
                return NotFound(); // R7
            }

            return View(new FrameworkVersionsViewModel(framework, organization!));
        }

        // Åbn PDF'en for en bestemt version
        [HttpGet]
        public async Task<IActionResult> Document(Guid id, CancellationToken cancellationToken)
        {
            var organization = await MyOrganizationAsync(cancellationToken);
            var document = organization is null ? null : await _frameworks.GetDocumentAsync(organization.Id, id, cancellationToken);
            if (document is null)
            {
                return NotFound(); // R7: også når PDF'en tilhører en anden organisation
            }

            // ASVS V5.4.1: altid som PDF og med vores eget, rensede filnavn i Content-Disposition
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(document.Value.Content, "application/pdf", document.Value.Version.FileName);
        }

        // Fælles for "tilføj" og "ny version": læs filen, kald use casen, vis fejl eller gå tilbage til listen
        private async Task<IActionResult> UploadAsync(Func<byte[], Task> upload, IFormFile? file, CancellationToken cancellationToken)
        {
            try
            {
                if (file is null || file.Length == 0)
                {
                    throw new DomainException("Choose a PDF file");
                }

                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                await upload(buffer.ToArray());

                return RedirectToAction(nameof(Index)); // Post/Redirect/Get
            }
            catch (DomainException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var organization = await MyOrganizationAsync(cancellationToken);
                return View(nameof(Index), await PageAsync(organization!, cancellationToken));
            }
        }

        private async Task<FrameworksPageViewModel> PageAsync(Organization organization, CancellationToken cancellationToken)
            => new(organization, await _frameworks.ListAsync(organization.Id, cancellationToken));

        private Task<Organization?> MyOrganizationAsync(CancellationToken cancellationToken)
            => _organizations.GetByMemberAsync(User.ExternalId(), cancellationToken);
    }
}