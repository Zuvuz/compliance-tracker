using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Reports
{
    public interface IStatusTextRepository
    {
        Task<IReadOnlyList<StatusText>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(StatusText text, CancellationToken cancellationToken = default);
    }

    // US-040 R2, R3: the data a text is written from: the tracker's own figures, never the AI's. Pure, so it is tested on
    // its own. The fingerprint of a part's data is the text's Basis: when it changes, the text is outdated.
    public static class StatusReportData
    {
        public const int MaxGapsNamed = 10;

        // e.g. "ISO/IEC 27001 version 1: 31 of 93 requirements covered, 12 with confirmed evidence, 62 not covered (A.5.7, A.5.23)"
        public static string Of(FrameworkCompliance framework)
        {
            var proven = framework.Requirements.Count(r => r.IsApplicable && !r.Requirement.IsRecommendation && r.IsCovered && r.IsProven);
            var gaps = framework.Requirements.Where(r => r.IsApplicable && !r.Requirement.IsRecommendation && !r.IsCovered)
                .Select(r => r.Requirement.Code).ToList();
            var named = gaps.Count == 0 ? "" : $" ({string.Join(", ", gaps.Take(MaxGapsNamed))}{(gaps.Count > MaxGapsNamed ? ", …" : "")})";
            return string.Create(CultureInfo.InvariantCulture,
                $"{FrameworkSlugs.Short(framework.Framework.Name)} version {framework.Version.Number}: {framework.Covered} of {framework.Total} requirements covered, {proven} with confirmed evidence, {framework.NotCovered} not covered{named}");
        }

        // The summary's data: every regulation, and the open risks, findings and events
        public static string OfAll(IReadOnlyList<FrameworkCompliance> frameworks, ManagementFacts facts)
            => string.Join("\n", frameworks.Select(Of)) + "\n" + string.Create(CultureInfo.InvariantCulture,
                $"Open High and Critical risks: {facts.OpenHighRisks}. Open severe scan findings: {facts.OpenSevereFindings}. Open security events: {facts.OpenSecurityEvents}.");

        // The part's current Basis; null for a regulation the project no longer follows
        public static string? BasisOf(string part, IReadOnlyList<FrameworkCompliance> frameworks, ManagementFacts facts)
            => part == StatusText.Summary ? Fingerprint(OfAll(frameworks, facts))
                : frameworks.FirstOrDefault(f => f.Framework.Name == part) is { } framework ? Fingerprint(Of(framework)) : null;

        public static string Fingerprint(string data) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)))[..16].ToLowerInvariant();

        public static string Label(string part) => part == StatusText.Summary ? "Summary" : FrameworkSlugs.Short(part);
    }

    // US-040 R3: the prompt gives the AI the figures and asks only for words; the numbers in the report never come from it
    public static class StatusReportPrompt
    {
        public const string Version = "status-report-v1";
        public const string Marker = "Write the text parts of a compliance status report";

        public sealed record Answer(string? Summary, IReadOnlyDictionary<string, string> Texts);

        public static string For(string projectName, IReadOnlyList<FrameworkCompliance> frameworks, ManagementFacts facts)
        {
            var text = new StringBuilder();
            text.AppendLine($"{Marker} for management, for the product {projectName}.");
            text.AppendLine("Use only the figures below. Do not invent numbers, requirements or plans. Plain words, no lists, at most three sentences each.");
            text.AppendLine("Figures:");
            text.AppendLine(StatusReportData.OfAll(frameworks, facts));
            text.AppendLine($"Write one TEXT line for each of: {string.Join(", ", frameworks.Select(f => FrameworkSlugs.Short(f.Framework.Name)))}");
            text.AppendLine("Answer only in exactly this shape:");
            text.AppendLine("SUMMARY: <the overall status>");
            text.Append("TEXT <regulation>: <its status>");
            return text.ToString();
        }

        // Lines of another shape, and regulations the project does not follow, are left out
        public static Answer Parse(string answer, IReadOnlyList<FrameworkCompliance> frameworks)
        {
            string? summary = null;
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            var byShort = frameworks.GroupBy(f => FrameworkSlugs.Short(f.Framework.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Framework.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var line in (answer ?? "").Split('\n').Select(l => l.Trim().TrimStart('*', '-', ' ')))
            {
                if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
                {
                    summary = Cut(line["SUMMARY:".Length..]) ?? summary;
                }
                else if (line.StartsWith("TEXT ", StringComparison.OrdinalIgnoreCase))
                {
                    // A name may hold a colon itself ("ISO/IEC 27001:2022"), so the longest known name that is followed by one wins
                    var rest = line[5..].TrimStart();
                    var known = byShort.Keys.Where(k => rest.StartsWith(k, StringComparison.OrdinalIgnoreCase) && rest[k.Length..].TrimStart().StartsWith(':'))
                        .OrderByDescending(k => k.Length).FirstOrDefault();
                    if (known is not null && Cut(rest[(rest.IndexOf(':', known.Length) + 1)..]) is { } said)
                    {
                        texts[byShort[known]] = said;
                    }
                }
            }

            return new Answer(summary, texts);
        }

        private static string? Cut(string text)
        {
            var cleaned = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return cleaned.Length == 0 ? null : cleaned.Length <= StatusText.MaxLength ? cleaned : cleaned[..StatusText.MaxLength];
        }
    }

    // One text as the Reports tab and the report show it
    public sealed record StatusTextLine(Guid Id, string Part, string Label, string Text, bool IsAiGenerated, bool IsOutdated, string? Model, DateTime WrittenAt);

    public sealed record StatusTextsView(IReadOnlyList<StatusTextLine> Texts, bool CanChange)
    {
        public bool Unfinished => Texts.Any(t => t.IsAiGenerated || t.IsOutdated);
    }

    // US-040: draft, accept and edit the management report's texts. Reading is for every member (R8); changing needs
    // DecideCompliance, the same as locking.
    public class StatusReportService
    {
        public const string NotAccepted = "Accept or edit every AI-generated text first";
        public const string Outdated = "The figures changed since a text was written: draft again or accept it again first";

        private readonly IStatusTextRepository _texts;
        private readonly ReportFiles _files;
        private readonly CurrentMember _member;
        private readonly IRationaleWriter _writer;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public StatusReportService(IStatusTextRepository texts, ReportFiles files, CurrentMember member, IAiModels models, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _texts = texts;
            _files = files;
            _member = member;
            _writer = models.Writer(AiTask.StatusReport);
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // Null for another organisation's project
        // The figures are only read when there are texts to compare with them, so the Reports tab stays as quick as before
        public async Task<StatusTextsView?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return null;
            }

            var texts = await _texts.ListAsync(projectId, cancellationToken);
            var canChange = await _member.CanAsync(Permission.DecideCompliance, cancellationToken);
            if (texts.Count == 0)
            {
                return new StatusTextsView([], canChange);
            }

            return await _files.ManagementInputAsync(projectId, cancellationToken) is { } input
                ? new StatusTextsView(Lines(texts, input.Frameworks, input.Facts), canChange)
                : null;
        }

        // The texts in report order: the summary, then the regulations in the project's order; texts of regulations the
        // project no longer follows are left out
        public static IReadOnlyList<StatusTextLine> Lines(IReadOnlyList<StatusText> texts, IReadOnlyList<FrameworkCompliance> frameworks, ManagementFacts facts)
            => texts
                .Select(t => (Text: t, Basis: StatusReportData.BasisOf(t.Part, frameworks, facts)))
                .Where(x => x.Basis is not null)
                .OrderBy(x => x.Text.IsSummary ? -1 : frameworks.ToList().FindIndex(f => f.Framework.Name == x.Text.Part))
                .Select(x => new StatusTextLine(x.Text.Id, x.Text.Part, StatusReportData.Label(x.Text.Part), x.Text.Text, x.Text.IsAiGenerated,
                    x.Text.IsOutdated(x.Basis!), x.Text.Model, x.Text.WrittenAt))
                .ToList();

        // R1, R5: the AI writes the summary and a text per regulation from today's figures; drafting again replaces them
        public async Task<int> DraftAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (_, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var input = await _files.ManagementInputAsync(projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (input.Project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            if (input.Frameworks.Count == 0)
            {
                throw new DomainException("Add a regulation to the project first");
            }

            // An AiUnavailableException reaches the page, and nothing changes
            var answer = StatusReportPrompt.Parse(await _writer.WriteAsync(StatusReportPrompt.For(input.Project.Name, input.Frameworks, input.Facts), cancellationToken),
                input.Frameworks);
            var written = new List<(string Part, string Text)>();
            if (answer.Summary is { } summary)
            {
                written.Add((StatusText.Summary, summary));
            }

            written.AddRange(answer.Texts.Select(t => (t.Key, t.Value)));
            if (written.Count == 0)
            {
                throw new DomainException("The AI's answer could not be read; try again");
            }

            var existing = await _texts.ListAsync(projectId, cancellationToken);
            foreach (var (part, text) in written)
            {
                var basis = StatusReportData.BasisOf(part, input.Frameworks, input.Facts)!;
                if (existing.FirstOrDefault(t => t.Part == part) is { } old)
                {
                    old.Redraft(text, basis, _writer.Name, Now);
                }
                else
                {
                    await _texts.AddAsync(StatusText.Draft(projectId, part, text, basis, _writer.Name, Now), cancellationToken);
                }
            }

            // R9
            await _log.LogAsync(ActivityAction.ReportDrafted, projectId,
                $"Management report texts drafted by {_writer.Name} ({StatusReportPrompt.Version}): {string.Join(", ", written.Select(w => StatusReportData.Label(w.Part)))}",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return written.Count;
        }

        // R4: accepted as it is, for today's figures
        public async Task AcceptAsync(Guid projectId, Guid textId, CancellationToken cancellationToken = default)
            => await ChangeAsync(projectId, textId, "Accepted", (text, user, basis) => text.Accept(user, basis, Now), cancellationToken);

        // R4: a person's own words
        public async Task EditAsync(Guid projectId, Guid textId, string? text, CancellationToken cancellationToken = default)
            => await ChangeAsync(projectId, textId, "Edited", (t, user, basis) => t.Edit(text ?? "", user, basis, Now), cancellationToken);

        private async Task ChangeAsync(Guid projectId, Guid textId, string what, Action<StatusText, Guid, string> change, CancellationToken cancellationToken)
        {
            var (_, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var input = await _files.ManagementInputAsync(projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (input.Project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var text = (await _texts.ListAsync(projectId, cancellationToken)).FirstOrDefault(t => t.Id == textId)
                       ?? throw new DomainException("Text not found");
            var basis = StatusReportData.BasisOf(text.Part, input.Frameworks, input.Facts)
                        ?? throw new DomainException("The project no longer follows this regulation");
            change(text, user.Id, basis);
            await _log.LogAsync(ActivityAction.ReportTextChanged, projectId, $"{what}: {StatusReportData.Label(text.Part)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5: why a report with these texts cannot be locked yet; null when it can
        public static string? Blocker(IReadOnlyList<StatusTextLine> shown)
            => shown.Any(t => t.IsAiGenerated) ? NotAccepted : shown.Any(t => t.IsOutdated) ? Outdated : null;
    }
}
