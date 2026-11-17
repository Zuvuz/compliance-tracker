using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-045: Users in the top bar. The members and their roles (US-010) and the invitations (US-009).
    // Only for roles with ManageMembers; the use cases check the permission again on the server (US-010 R5).
    public class UsersController : Controller
    {
        private readonly OrganizationQueries _organizations;
        private readonly MemberService _members;
        private readonly MemberQueries _memberQueries;
        private readonly CurrentMember _currentMember;

        public UsersController(
            OrganizationQueries organizations,
            MemberService members,
            MemberQueries memberQueries,
            CurrentMember currentMember)
        {
            _organizations = organizations;
            _members = members;
            _memberQueries = memberQueries;
            _currentMember = currentMember;
        }

        // GET /Users
        [HttpGet]
        public async Task<IActionResult> Index([FromServices] Application.Projects.ProjectQueries projects, [FromServices] TimeProvider clock,
            CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            if (!await _currentMember.CanAsync(Permission.ManageMembers, cancellationToken))
            {
                return Forbid(); // US-045 R2
            }

            return View(new MembersViewModel(organization, await _memberQueries.OpenInvitationsAsync(cancellationToken))
            {
                Projects = (await projects.ListAsync(cancellationToken)).Where(p => !p.IsOrganisationArea).OrderBy(p => p.Name).ToList(),
                Today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)
            });
        }

        // GET /Users/MyData: US-095 R2, for every member (not only those who manage members)
        [HttpGet]
        public async Task<IActionResult> MyData([FromServices] Application.Organizations.PersonalDataService personalData, CancellationToken cancellationToken)
        {
            if (await _organizations.MineAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(await personalData.ExportMineAsync(cancellationToken), "application/json", "my-data.json");
        }

        // POST /Users/ErasePersonalData: US-095 R3, a removed member's name and e-mail become a pseudonym
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ErasePersonalData(Guid memberId, [FromServices] Application.Organizations.PersonalDataService personalData, CancellationToken cancellationToken)
        {
            var erased = await personalData.EraseAsync(memberId, cancellationToken);
            TempData["PersonalDataErased"] = $"The personal data is erased; the member is now \"{erased.Pseudonym}\". Remove the account in Keycloak too.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Users/ChangeRole: US-010 R2, R3, R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> ChangeRole(Guid memberId, UserRole role, CancellationToken cancellationToken)
        {
            await _members.ChangeRoleAsync(memberId, role, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Users/SetSignsForManagement: US-132 R3, who signs reports for the management
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> SetSignsForManagement(
            Guid memberId, bool signs, [FromServices] Application.Reports.ReportSigningService signing, CancellationToken cancellationToken)
        {
            await signing.SetSignsForManagementAsync(memberId, signs, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Users/Remove: US-090 R1, R2. The member's access ends at once; their history keeps their name.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Remove(Guid memberId, CancellationToken cancellationToken)
        {
            await _members.RemoveAsync(memberId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // POST /Users/CreateInvitation: US-009 R1. The link is shown once (only its hash is stored).
        // US-196: with the invitee's e-mail (the link is mailed at once), and for Consultant / Auditor an end date and a project.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> CreateInvitation(UserRole role, string? email, DateOnly? accessUntil, Guid? onlyProjectId,
            CancellationToken cancellationToken)
        {
            var external = RolePermissions.IsExternal(role);
            var result = await _members.CreateInvitationAsync(role, email, external ? accessUntil : null,
                role == UserRole.Auditor ? onlyProjectId : null, cancellationToken);
            TempData["InvitationLink"] = Url.Action("Accept", "Invitations", new { token = result.Token }, Request.Scheme);
            if (result.Mailed)
            {
                TempData["InvitationMailed"] = email?.Trim();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST /Users/SetAccessUntil: US-196 R5, the last day of a consultant's or an auditor's access
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> SetAccessUntil(Guid memberId, DateOnly? accessUntil, CancellationToken cancellationToken)
        {
            if (accessUntil is not { } until)
            {
                throw new Domain.Common.DomainException("Choose the last day of access");
            }

            await _members.SetAccessUntilAsync(memberId, until, cancellationToken);
            TempData["Success"] = $"Access until {until:dd-MM-yyyy}"; // US-171
            return RedirectToAction(nameof(Index));
        }

        // POST /Users/RevokeInvitation: US-009 R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> RevokeInvitation(Guid invitationId, CancellationToken cancellationToken)
        {
            await _members.RevokeInvitationAsync(invitationId, cancellationToken);
            return RedirectToAction(nameof(Index));
        }
    }
}
