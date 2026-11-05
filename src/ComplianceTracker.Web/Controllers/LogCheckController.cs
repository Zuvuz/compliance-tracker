using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-091 R3, R5: /LogCheck goes through the organisation's chain of log entries and says whether it is intact.
    // For Admins and Auditors; others get "forbidden".
    public class LogCheckController : Controller
    {
        private readonly ActivityLogIntegrityQueries _integrity;
        private readonly OrganizationQueries _organizations;

        public LogCheckController(ActivityLogIntegrityQueries integrity, OrganizationQueries organizations)
        {
            _integrity = integrity;
            _organizations = organizations;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            var check = await _integrity.CheckAsync(cancellationToken);
            if (check is null)
            {
                return Forbid();
            }

            ViewData["Title"] = "Check the activity log";
            ViewData["Organization"] = organization;
            return View(check);
        }
    }
}
