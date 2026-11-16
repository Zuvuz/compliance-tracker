using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-191: the organisation's suppliers, under "Organisation" in the top bar
    public class SuppliersController : Controller
    {
        private const long MaxRequestSize = Domain.Mappings.EvidenceFile.MaxSizeInBytes + 1024 * 1024;

        private readonly SupplierService _suppliers;
        private readonly CurrentMember _member;

        public SuppliersController(SupplierService suppliers, CurrentMember member)
        {
            _suppliers = suppliers;
            _member = member;
        }

        // GET /Suppliers
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            ViewData["OrganisationArea"] = true;
            return View(new SuppliersViewModel(await _suppliers.ListAsync(cancellationToken), await _member.CanAsync(Permission.DecideCompliance, cancellationToken)));
        }

        // POST /Suppliers/Add: R1, R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Add(string? name, string? delivers, SupplierCriticality? criticality, string? contact, List<string>? rules,
            CancellationToken cancellationToken)
        {
            await _suppliers.AddAsync(name, delivers, criticality, contact, rules, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Suppliers/AddEvidence: R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> AddEvidence(Guid supplierId, SupplierEvidenceKind? kind, string? title, DateOnly? validUntil, IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                throw new Domain.Common.DomainException("Choose a file");
            }

            using var memory = new MemoryStream();
            await file.CopyToAsync(memory, cancellationToken);
            await _suppliers.AddEvidenceAsync(supplierId, kind, title, validUntil, file.FileName, memory.ToArray(), cancellationToken);
            if (validUntil is null)
            {
                TempData["NoReviewDate"] = Domain.Organizations.ReviewDefaults.NoDateWarning; // US-195 R2
            }
            return RedirectToAction(nameof(Index), null, $"supplier-{supplierId}");
        }

        // POST /Suppliers/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Remove(Guid supplierId, CancellationToken cancellationToken)
        {
            await _suppliers.RemoveAsync(supplierId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // GET /Suppliers/File?supplierId=…&supplierEvidenceId=…: for any member of the organisation
        public async Task<IActionResult> File(Guid supplierId, Guid supplierEvidenceId, CancellationToken cancellationToken)
            => await _suppliers.FileAsync(supplierId, supplierEvidenceId, cancellationToken) is { } file
                ? File(file.Content, file.ContentType, file.FileName)
                : NotFound();
    }

    public sealed record SuppliersViewModel(IReadOnlyList<SupplierView> Suppliers, bool CanManage);
}
