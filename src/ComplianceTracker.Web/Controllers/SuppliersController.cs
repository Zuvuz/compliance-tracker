using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-191: the organisation's suppliers, under "Organisation" in the top bar
    // US-207: [Authorize] says it here as well as in the global policy, so CodeQL sees the check
    [Authorize]
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
        public async Task<IActionResult> Index([FromServices] Application.Isms.AssetService assets, CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            ViewData["SupplierAssets"] = await assets.BySupplierAsync(cancellationToken); // US-214
            await Models.PageVisits.StartVisitAsync(this, null, Domain.ActivityLog.VisitKind.Supplier, null, "suppliers", "Suppliers", cancellationToken); // US-219 R1
            var organization = await _member.FindOrganizationAsync(cancellationToken);
            return View(new SuppliersViewModel(await _suppliers.ListAsync(cancellationToken), await _member.CanAsync(Permission.DecideCompliance, cancellationToken))
            {
                OrganizationName = organization?.Name ?? "" // US-207 R4: the subject of "Write"
            });
        }

        // POST /Suppliers/Add: R1, R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Add(string? name, string? delivers, SupplierCriticality? criticality, string? contact, List<string>? rules,
            string? registrationNumber, string? address, string? country, string? website, string? securityEmail,
            CancellationToken cancellationToken)
        {
            var supplier = await _suppliers.AddAsync(name, delivers, criticality, contact, rules, cancellationToken,
                new CompanyDetails(registrationNumber, address, country, website, securityEmail)); // US-207 R1
            return RedirectToAction(nameof(Index), null, $"supplier-{supplier.Id}");
        }

        // POST /Suppliers/ChangeCompany: US-207 R1
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ChangeCompany(Guid supplierId, string? registrationNumber, string? address, string? country, string? website,
            string? securityEmail, CancellationToken cancellationToken)
        {
            await _suppliers.ChangeCompanyAsync(supplierId, new CompanyDetails(registrationNumber, address, country, website, securityEmail), cancellationToken);
            return RedirectToAction(nameof(Index), null, $"supplier-{supplierId}");
        }

        // POST /Suppliers/AddContact: US-207 R2, R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> AddContact(Guid supplierId, string? name, string? role, string? email, string? phone, bool primary,
            bool securityContact, CancellationToken cancellationToken)
        {
            await _suppliers.AddContactAsync(supplierId, new ContactDetails(name, role, email, phone, primary, securityContact), cancellationToken);
            return RedirectToAction(nameof(Index), null, $"supplier-{supplierId}");
        }

        // POST /Suppliers/ChangeContact: US-207 R2, R3
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ChangeContact(Guid supplierId, Guid contactId, string? name, string? role, string? email, string? phone,
            bool primary, bool securityContact, CancellationToken cancellationToken)
        {
            await _suppliers.ChangeContactAsync(supplierId, contactId, new ContactDetails(name, role, email, phone, primary, securityContact), cancellationToken);
            return RedirectToAction(nameof(Index), null, $"supplier-{supplierId}");
        }

        // POST /Suppliers/RemoveContact: US-207 R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> RemoveContact(Guid supplierId, Guid contactId, CancellationToken cancellationToken)
        {
            await _suppliers.RemoveContactAsync(supplierId, contactId, cancellationToken);
            return RedirectToAction(nameof(Index), null, $"supplier-{supplierId}");
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

    public sealed record SuppliersViewModel(IReadOnlyList<SupplierView> Suppliers, bool CanManage)
    {
        public string OrganizationName { get; init; } = "";

        // US-207 R4: the subject of "Write": security for a security contact or the security mailbox (null), updated
        // evidence when it is missing or ends soon, else the organisation's name, e.g. "Security – Demo ApS"
        public static string Subject(SupplierView view, SupplierContact? contact, string organization)
            => contact is null || contact.IsSecurityContact ? $"Security{Suffix(organization)}"
                : view.Status.State != SupplierState.Ok ? EvidenceSubject(organization)
                : string.IsNullOrWhiteSpace(organization) ? view.Supplier.Name : organization;

        // US-207: asking the primary contact for evidence that is missing or ends soon
        public static string EvidenceSubject(string organization) => $"Updated evidence{Suffix(organization)}";

        private static string Suffix(string organization) => string.IsNullOrWhiteSpace(organization) ? "" : $" – {organization}";
    }
}
