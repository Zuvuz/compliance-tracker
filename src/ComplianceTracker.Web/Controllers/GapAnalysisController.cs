using System.Text.Json;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Controllers
{
    // US-188: the NIS2 gap analysis, under "Organisation" in the top bar. The customer's documents are uploaded here (a ZIP
    // or several files); the AI suggests the NIS2 requirements each covers, quoting the document; a person decides.
    public class GapAnalysisController : Controller
    {
        // A ZIP of a customer's documents may be large; GapFiles limits what is read from it
        private const long MaxRequestSize = GapFiles.MaxTotalBytes + 10 * 1024 * 1024;
        private const int MaxResultLines = 100;
        private const string ResultKey = "GapUpload";

        private readonly GapAnalysisService _gap;
        private readonly DocumentSuggestionService _suggestions;
        private readonly GapQuestionService _questions;
        private readonly Nis2GapReportService _report;
        private readonly CurrentMember _member;

        public GapAnalysisController(GapAnalysisService gap, DocumentSuggestionService suggestions, GapQuestionService questions,
            Nis2GapReportService report, CurrentMember member)
        {
            _gap = gap;
            _suggestions = suggestions;
            _questions = questions;
            _report = report;
            _member = member;
        }

        // GET /GapAnalysis
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return RedirectToAction("Create", "Organizations");
            }

            ViewData["OrganisationArea"] = true;
            var (areaId, lines, reports) = await _report.CurrentAsync(cancellationToken); // US-190
            return View(new GapAnalysisViewModel(
                await _suggestions.PendingForAsync(IsNis2, cancellationToken),
                await _member.CanAsync(Permission.ManageFrameworks, cancellationToken),
                TempData[ResultKey] is string json ? JsonSerializer.Deserialize<GapUploadView>(json) : null)
            {
                Questions = await _questions.QuestionsAsync(cancellationToken), // US-189
                CanAnswer = await _member.CanAsync(Permission.DecideCompliance, cancellationToken),
                AreaId = areaId,
                Lines = lines,
                Reports = reports,
                CanAddRisks = await _member.CanAsync(Permission.ManageRisks, cancellationToken)
            });
        }

        // POST /GapAnalysis/Report: US-190 R3, the locked gap report; any member may make it
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Report(CancellationToken cancellationToken)
        {
            await _report.MakeAsync(cancellationToken);
            return RedirectToAction(nameof(Index), null, "report");
        }

        // POST /GapAnalysis/Answer: US-189 R3, R5, one question at a time; back to the question
        [HttpPost]
        [ValidateAntiForgeryToken]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Answer(string? code, Domain.Organizations.Maturity? maturity, string? comment, CancellationToken cancellationToken)
        {
            await _questions.AnswerAsync(code ?? "", maturity, comment, cancellationToken);
            return RedirectToAction(nameof(Index), null, "questions");
        }

        // POST /GapAnalysis/Upload: R1, R4
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
        [OnDomainError(nameof(Index))]
        public async Task<IActionResult> Upload(List<IFormFile>? files, CancellationToken cancellationToken)
        {
            var read = new List<(string, byte[])>();
            foreach (var file in files ?? [])
            {
                using var memory = new MemoryStream();
                await file.CopyToAsync(memory, cancellationToken);
                read.Add((file.FileName, memory.ToArray()));
            }

            var result = await _gap.UploadAsync(read, cancellationToken);
            TempData[ResultKey] = JsonSerializer.Serialize(new GapUploadView(result.Summary,
                result.Files.Take(MaxResultLines).Select(f => new GapUploadLine(f.FileName, f.Outcome.ToString(), f.Note)).ToList(),
                Math.Max(0, result.Files.Count - MaxResultLines)));
            return RedirectToAction(nameof(Index));
        }

        // NIS2 itself, the Danish NIS2 law and the implementing regulation
        private static bool IsNis2(string framework) => framework.StartsWith("NIS", StringComparison.OrdinalIgnoreCase);
    }

    public sealed record GapUploadLine(string FileName, string Outcome, string Note);

    public sealed record GapUploadView(string Summary, IReadOnlyList<GapUploadLine> Lines, int More);

    public sealed record GapAnalysisViewModel(IReadOnlyList<GapSuggestionView> Suggestions, bool CanManage, GapUploadView? LastUpload)
    {
        // US-189
        public IReadOnlyList<GapQuestionView> Questions { get; init; } = [];
        public bool CanAnswer { get; init; }

        // US-190
        public Guid AreaId { get; init; }
        public IReadOnlyList<GapLine> Lines { get; init; } = [];
        public IReadOnlyList<Domain.Reports.ReportSnapshot> Reports { get; init; } = [];
        public bool CanAddRisks { get; init; }
    }
}
