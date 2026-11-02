using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-010: the tracker owner's pages. All organisations and their members, and role changes.
    // Never an organisation's projects, frameworks or other data. Only for the Keycloak realm role "system-admin".
    [Authorize(Policy = SystemAdmin.Policy)]
    public class SystemController : Controller
    {
        private readonly SystemAdminService _system;

        public SystemController(SystemAdminService system)
        {
            _system = system;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
            => View(await _system.ListOrganizationsAsync(cancellationToken));

        [HttpGet]
        public async Task<IActionResult> Organization(Guid id, CancellationToken cancellationToken)
            => await _system.GetOrganizationAsync(id, cancellationToken) is { } organization ? View(organization) : NotFound();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Organization), IdFrom = "id")]
        public async Task<IActionResult> ChangeRole(Guid id, Guid memberId, UserRole role, CancellationToken cancellationToken)
        {
            await _system.ChangeRoleAsync(id, memberId, role, cancellationToken);
            return RedirectToAction(nameof(Organization), new { id });
        }
    }
}
