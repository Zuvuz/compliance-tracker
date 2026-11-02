using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // Thin controller: it only translates between the pages and the Application layer (US-003 R6, US-029).
    // Use cases change data, queries read it; both work for the signed-in user only.
    // A broken rule is shown by DomainExceptionFilter, so there is no try/catch here.
    // Requires sign-in through the FallbackPolicy in Program.cs (US-004 R1).
    public class OrganizationsController : Controller
    {
        private readonly OrganizationService _service;
        private readonly OrganizationQueries _organizations;
        private readonly ProjectQueries _projects;
        private readonly FrameworkQueries _frameworks;

        private readonly TimeProvider _clock;

        private readonly AiFeedbackService _aiFeedback;
        private readonly MemberService _members;
        private readonly MemberQueries _memberQueries;

        public OrganizationsController(
            OrganizationService service,
            AiFeedbackService aiFeedback,
            MemberService members,
            MemberQueries memberQueries,
            OrganizationQueries organizations,
            ProjectQueries projects,
            FrameworkQueries frameworks,
            TimeProvider clock)
        {
            _clock = clock;
            _aiFeedback = aiFeedback;
            _members = members;
            _memberQueries = memberQueries;
            _service = service;
            _organizations = organizations;
            _projects = projects;
            _frameworks = frameworks;
        }

        // "My organisation": the user's own, or the create page on first sign-in (US-004 R3)
        [HttpGet]
        public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            return organization is null
                ? RedirectToAction(nameof(Create))
                : RedirectToAction(nameof(Details), new { id = organization.Id });
        }

        // GET: shows the empty create form
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // US-004 R4: if the user already has an organisation, show that instead
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is not null)
            {
                return RedirectToAction(nameof(Details), new { id = organization.Id });
            }

            return View(new CreateOrganizationViewModel());
        }

        // POST: creates the organisation from the form. The creator is the signed-in user.
        [HttpPost]
        [ValidateAntiForgeryToken] // US-003 R5: only forms from the app itself (ASVS V3.5.1)
        public async Task<IActionResult> Create(CreateOrganizationViewModel model, CancellationToken cancellationToken)
        {
            // An error here means someone sent a value the form cannot produce (e.g. an unknown sector)
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var organization = await _service.CreateAsync(
                model.Name ?? "",
                model.Cvr ?? "",
                model.Sector,
                model.Size,
                cancellationToken);

            // US-003 R2: show the new organisation (Post/Redirect/Get, so a reload does not create it again)
            return RedirectToAction(nameof(Details), new { id = organization.Id });
        }

        // The Overview tab: name, CVR, the organisation's projects (US-008 R4)
        // and which framework versions they use (US-011 R5)
        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            var organization = await _organizations.GetAsync(id, cancellationToken);
            if (organization is null)
            {
                return NotFound(); // US-004 R5: also when the organisation belongs to someone else
            }

            return View(new OrganizationOverviewViewModel(
                organization,
                await _projects.ListAsync(cancellationToken),
                await _frameworks.ListAsync(cancellationToken),
                _clock.Today()));
        }

        // The Users tab: the members and their roles
        [HttpGet]
        public async Task<IActionResult> Users(Guid id, CancellationToken cancellationToken)
        {
            var organization = await _organizations.GetAsync(id, cancellationToken);
            return organization is null
                ? NotFound()
                : View(new MembersViewModel(organization, await _memberQueries.OpenInvitationsAsync(cancellationToken)));
        }

        // ---------------------------------------------------------- Roles and invitations (US-009, US-010)

        // POST /Organizations/ChangeRole: R2, R3, R6
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Users), IdFrom = "id")]
        public async Task<IActionResult> ChangeRole(Guid id, Guid memberId, UserRole role, CancellationToken cancellationToken)
        {
            await _members.ChangeRoleAsync(memberId, role, cancellationToken);
            return RedirectToAction(nameof(Users), new { id });
        }

        // POST /Organizations/CreateInvitation: US-009 R1. The link is shown once (only its hash is stored).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Users), IdFrom = "id")]
        public async Task<IActionResult> CreateInvitation(Guid id, UserRole role, CancellationToken cancellationToken)
        {
            var token = await _members.CreateInvitationAsync(role, cancellationToken);
            TempData["InvitationLink"] = Url.Action("Accept", "Invitations", new { token }, Request.Scheme);
            return RedirectToAction(nameof(Users), new { id });
        }

        // POST /Organizations/RevokeInvitation: US-009 R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Users), IdFrom = "id")]
        public async Task<IActionResult> RevokeInvitation(Guid id, Guid invitationId, CancellationToken cancellationToken)
        {
            await _members.RevokeInvitationAsync(invitationId, cancellationToken);
            return RedirectToAction(nameof(Users), new { id });
        }

        // ---------------------------------------------------------- AI feedback (US-036)

        // POST /Organizations/SetAiSharing: R5, R6. Only an Admin (checked in the domain).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> SetAiSharing(Guid id, bool share, CancellationToken cancellationToken)
        {
            await _aiFeedback.SetSharingAsync(share, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Organizations/ExportAiFeedback: R7–R9. JSON Lines, one decision per line, without names.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Details), IdFrom = "id")]
        public async Task<IActionResult> ExportAiFeedback(Guid id, CancellationToken cancellationToken)
        {
            var lines = await _aiFeedback.ExportAsync(cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(lines), "application/x-ndjson", "ai-feedback.jsonl");
        }

    }
}
