using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-102: the whole organisation's activity log at /Activity, for Admins and Auditors (R6).
    // Searches, filters and pages like a project's Activity tab (US-101), plus a filter by project (R3).
    public class ActivityController : Controller
    {
        private readonly ActivityLogQueries _log;
        private readonly OrganizationQueries _organizations;

        public ActivityController(ActivityLogQueries log, OrganizationQueries organizations)
        {
            _log = log;
            _organizations = organizations;
        }

        // GET /Activity?q=...&from=...&to=...&group=...&user=...&sort=...&page=...&project=...
        [HttpGet]
        public async Task<IActionResult> Index(
            string? q, string? from, string? to, string? group, string? user, string? sort, string? page, string? project,
            CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            var (query, parsed) = ActivityFilters.Parse(q, from, to, group, user, sort, page);
            var ignored = parsed.ToList();
            var (scope, projectId, projectFilter) = Project(project, ignored);
            var result = await _log.ForOrganizationAsync(query, scope, projectId, cancellationToken);
            if (result is null)
            {
                return Forbid(); // R6: only Admins and Auditors
            }

            ViewData["Title"] = "Activity";
            return View(new ActivityLogViewModel(
                "organisation-activity",
                Url.Action(nameof(Index))!,
                query,
                result.Page,
                ignored,
                organization,
                result.Subjects,
                result.Projects,
                projectFilter));
        }

        // GET /Activity/Export?format=csv|pdf&...: US-055, the entries the same filters find; the last 90 days without dates
        [HttpGet]
        public async Task<IActionResult> Export(
            string? format, string? q, string? from, string? to, string? group, string? user, string? project,
            [FromServices] ActivityLogExportService export, CancellationToken cancellationToken)
        {
            var (query, parsed) = ActivityFilters.Parse(q, from, to, group, user, "oldest", null);
            var ignored = parsed.ToList();
            var (scope, projectId, _) = Project(project, ignored);
            var file = await export.ExportAsync(query, scope, projectId,
                string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) ? ActivityExportFormat.Pdf : ActivityExportFormat.Csv, cancellationToken);
            return file is null ? Forbid() : File(file.Content, file.ContentType, file.FileName); // R4: Admins and Auditors (US-102 R6)
        }

        // R3: "none" = not about a project, a project's id = that project; anything else is ignored and said so
        private static (ActivityProjectScope Scope, Guid? ProjectId, string? Filter) Project(string? value, IList<string> ignored)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (ActivityProjectScope.All, null, null);
            }

            if (value.Equals(ActivityLogViewModel.NotAboutAProject, StringComparison.OrdinalIgnoreCase))
            {
                return (ActivityProjectScope.NotAboutAProject, null, ActivityLogViewModel.NotAboutAProject);
            }

            if (Guid.TryParse(value, out var id))
            {
                return (ActivityProjectScope.OneProject, id, id.ToString());
            }

            ignored.Add($"The project '{value}' was not understood and is ignored");
            return (ActivityProjectScope.All, null, null);
        }
    }
}
