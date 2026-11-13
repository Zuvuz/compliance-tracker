using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Audits;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Web.Filters;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-134: a project's (or the organisation's) audits at /Audits?projectId=...: record one with its report, findings and
    // certificate, follow the findings up, and download the auditor's report. Thin: AuditService does the work.
    public class AuditsController : Controller
    {
        private const long MaxReportRequestSize = 11 * 1024 * 1024;

        private readonly AuditService _audits;
        private readonly ProjectQueries _projects;
        private readonly OrganizationQueries _organizations;

        public AuditsController(AuditService audits, ProjectQueries projects, OrganizationQueries organizations)
        {
            _audits = audits;
            _projects = projects;
            _organizations = organizations;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid projectId, CancellationToken cancellationToken)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var organization = await _organizations.MineAsync(cancellationToken);
            var overview = await _audits.ForProjectAsync(projectId, cancellationToken);
            if (project is null || organization is null || overview is null)
            {
                return NotFound();
            }

            return View(new AuditsViewModel(project, organization, overview));
        }

        // POST /Audits/Record: R1–R5. Up to five findings, each with an owner and a deadline.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxReportRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxReportRequestSize)]
        public async Task<IActionResult> Record(
            Guid projectId, AuditKind kind, string? scope, DateOnly? heldOn, string? auditorName, string? auditorCompany, AuditResult result,
            string? certificateNumber, string? certificateIssuer, DateOnly? certificateValidFrom, DateOnly? certificateValidTo, Guid? correctsAuditId,
            string[]? findingTitle, Guid[]? findingOwner, DateOnly?[]? findingDue, IFormFile? file, CancellationToken cancellationToken)
        {
            try
            {
                using var memory = new MemoryStream();
                if (file is not null)
                {
                    await file.CopyToAsync(memory, cancellationToken);
                }

                var findings = (findingTitle ?? []).Select((title, i) => new AuditFindingInput(
                    title, findingOwner is { } owners && i < owners.Length ? owners[i] : Guid.Empty, findingDue is { } dues && i < dues.Length ? dues[i] : null)).ToList();
                var input = new AuditInput(kind, scope, heldOn, auditorName, auditorCompany, result,
                    certificateNumber, certificateIssuer, certificateValidFrom, certificateValidTo, correctsAuditId) { Findings = findings };
                var audit = await _audits.RecordAsync(projectId, input, file?.FileName, memory.ToArray(), cancellationToken);
                TempData["AuditRecorded"] = AuditService.Describe(audit);
            }
            catch (DomainException error)
            {
                TempData[DomainExceptionFilter.TempDataKey] = error.Message;
            }

            return RedirectToAction(nameof(Index), new { projectId });
        }

        // POST /Audits/CloseFinding: R2
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseFinding(Guid projectId, Guid findingId, string? reason, Guid? evidenceId, CancellationToken cancellationToken)
        {
            try
            {
                await _audits.CloseFindingAsync(projectId, findingId, reason, evidenceId, cancellationToken);
            }
            catch (DomainException error)
            {
                TempData[DomainExceptionFilter.TempDataKey] = error.Message;
            }

            return RedirectToAction(nameof(Index), new { projectId });
        }

        // GET /Audits/Report?projectId=...&auditId=...: the auditor's report, a download with our content type (ASVS V5.4.1)
        [HttpGet]
        public async Task<IActionResult> Report(Guid projectId, Guid auditId, CancellationToken cancellationToken)
        {
            if (await _audits.ReportAsync(projectId, auditId, cancellationToken) is not { } found)
            {
                return NotFound();
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(found.File.Content, found.File.ContentType, found.Audit.ReportFileName);
        }
    }
}
