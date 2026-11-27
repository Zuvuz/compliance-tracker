using System.Globalization;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-219: the access log at /AccessLog: who opened what, when and for how long (R5), and who is on a page now (R3).
    // Only Organisation admin, Compliance manager and Auditor (R4). The signs of life of an open page come here too.
    public class AccessLogController : Controller
    {
        private readonly AccessLogService _accessLog;
        private readonly OrganizationQueries _organizations;
        private readonly IProjectRepository _projects;
        private readonly TimeProvider _clock;

        public AccessLogController(AccessLogService accessLog, OrganizationQueries organizations, IProjectRepository projects, TimeProvider clock)
        {
            _accessLog = accessLog;
            _organizations = organizations;
            _projects = projects;
            _clock = clock;
        }

        // GET /AccessLog?project=...&user=...&kind=...&from=dd-MM-yyyy&to=dd-MM-yyyy
        [HttpGet]
        public async Task<IActionResult> Index(string? project, string? user, string? kind, string? from, string? to, CancellationToken cancellationToken)
        {
            var organization = await _organizations.MineAsync(cancellationToken);
            if (organization is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            var projectId = Guid.TryParse(project, out var p) ? p : (Guid?)null;
            var userId = Guid.TryParse(user, out var u) ? u : (Guid?)null;
            var visitKind = PageVisits.ParseKind(kind);
            var (fromDate, toDate) = (Date(from), Date(to));
            var view = await _accessLog.SearchAsync(projectId, userId, visitKind, fromDate, toDate, cancellationToken);
            if (view is null)
            {
                return Forbid(); // R4
            }

            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).OrderBy(x => x.IsOrganisationArea ? 0 : 1).ThenBy(x => x.Name).ToList();
            ViewData["Title"] = "Access log";
            return View(new AccessLogPageModel(view, organization, projects, projectId, userId, visitKind, fromDate, toDate,
                projectId is not null || await CanSeeChangesAsync(cancellationToken), // a project's change log is for everyone who reads it
                _clock.GetUtcNow().UtcDateTime));
        }

        // GET /AccessLog/Mine: US-219 R7, GDPR art. 15, the pages the signed-in member opened, for every role
        [HttpGet]
        public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        {
            if (await _organizations.MineAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            ViewData["Title"] = "My page visits";
            return View(await _accessLog.MineAsync(cancellationToken));
        }

        // GET /AccessLog/Export?project=...&from=...&to=...: R8, the access log as CSV for the same filters
        [HttpGet]
        public async Task<IActionResult> Export(string? project, string? from, string? to, [FromServices] CurrentMember member, CancellationToken cancellationToken)
        {
            if (!await _accessLog.CanSeeAsync(cancellationToken))
            {
                return Forbid();
            }

            var (organization, _) = await member.RequireAsync(cancellationToken);
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            var start = Date(from) ?? today.AddDays(-ActivityLogExportService.DefaultDays);
            var end = Date(to) ?? today;
            var rows = await _accessLog.ForPeriodAsync(organization, Guid.TryParse(project, out var p) ? p : null, start, end, cancellationToken);
            var file = CsvFile.Write(AccessLogService.Columns, AccessLogService.CsvRows(rows));
            return File(file, "text/csv; charset=utf-8", $"access-log-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv");
        }

        // POST /AccessLog/SignOfLife: R2, from an open page; nothing is shown, and a sign for someone else's visit changes nothing
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOfLife(Guid visitId, bool visible, bool ended, CancellationToken cancellationToken)
        {
            await _accessLog.SignOfLifeAsync(visitId, visible, ended, cancellationToken);
            return NoContent();
        }

        // POST /AccessLog/ReadNotice: R7, the member has read the notice about the access log; it is not shown again
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReadNotice(CancellationToken cancellationToken)
        {
            if (await _organizations.MineAsync(cancellationToken) is { AccessLogStartsOn: { } startsOn } organization)
            {
                Response.Cookies.Append(PageVisits.NoticeCookie(organization.Id, startsOn), "read", new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true, Expires = DateTimeOffset.UtcNow.AddYears(5)
                });
            }

            return RedirectToAction("Index", "Review");
        }

        private async Task<bool> CanSeeChangesAsync(CancellationToken cancellationToken)
            => await HttpContext.RequestServices.GetRequiredService<ActivityLogQueries>().CanSeeAllAsync(cancellationToken);

        // dd-MM-yyyy as everywhere in the tracker, or yyyy-MM-dd from a date field; anything else is no date
        private static DateOnly? Date(string? value)
            => DateOnly.TryParseExact(value?.Trim(), ["dd-MM-yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
