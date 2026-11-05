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
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
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

            return View(new MembersViewModel(organization, await _memberQueries.OpenInvitationsAsync(cancellationToken)));
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> CreateInvitation(UserRole role, CancellationToken cancellationToken)
        {
            var token = await _members.CreateInvitationAsync(role, cancellationToken);
            TempData["InvitationLink"] = Url.Action("Accept", "Invitations", new { token }, Request.Scheme);
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
