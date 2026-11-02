using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-009 R2: the page an invitation link leads to. Requires sign-in like every page, so a new person
    // registers or signs in with Keycloak first and comes back here.
    public class InvitationsController : Controller
    {
        private readonly MemberService _members;

        public InvitationsController(MemberService members)
        {
            _members = members;
        }

        // GET /Invitations/Accept/{token}: which organisation and role, or why the link cannot be used
        [HttpGet("Invitations/Accept/{token}")]
        public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
        {
            var preview = await _members.PreviewAsync(token, cancellationToken);
            return preview is null ? NotFound() : View(new InvitationViewModel(token, preview));
        }

        // POST /Invitations/Accept/{token}
        [HttpPost("Invitations/Accept/{token}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptPost(string token, CancellationToken cancellationToken)
        {
            await _members.AcceptInvitationAsync(token, cancellationToken);
            return RedirectToAction("Mine", "Organizations");
        }
    }
}
