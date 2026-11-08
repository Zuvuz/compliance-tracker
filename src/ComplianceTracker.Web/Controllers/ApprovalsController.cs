using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-135: approve, reject or withdraw a request; each goes back to the project's Overview
    public class ApprovalsController : Controller
    {
        private readonly ApprovalService _service;

        public ApprovalsController(ApprovalService service)
        {
            _service = service;
        }

        // POST /Approvals/Approve: R3, R4, R5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Details", Controller = "Projects", IdFrom = "projectId", Tab = ProjectTabs.Overview)]
        public async Task<IActionResult> Approve(Guid id, Guid projectId, CancellationToken cancellationToken)
        {
            await _service.ApproveAsync(id, cancellationToken);
            return Back(projectId);
        }

        // POST /Approvals/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Details", Controller = "Projects", IdFrom = "projectId", Tab = ProjectTabs.Overview)]
        public async Task<IActionResult> Reject(Guid id, Guid projectId, string? reason, CancellationToken cancellationToken)
        {
            await _service.RejectAsync(id, reason, cancellationToken);
            return Back(projectId);
        }

        // POST /Approvals/Withdraw
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError("Details", Controller = "Projects", IdFrom = "projectId", Tab = ProjectTabs.Overview)]
        public async Task<IActionResult> Withdraw(Guid id, Guid projectId, CancellationToken cancellationToken)
        {
            await _service.WithdrawAsync(id, cancellationToken);
            return Back(projectId);
        }

        private RedirectToActionResult Back(Guid projectId)
            => RedirectToAction("Details", "Projects", new { id = projectId, tab = ProjectTabs.Overview });
    }
}
