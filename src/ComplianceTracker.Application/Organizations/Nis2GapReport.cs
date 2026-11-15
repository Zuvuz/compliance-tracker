using System.Text.Json;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Reports;

namespace ComplianceTracker.Application.Organizations
{
    // US-190 R1
    public enum GapStatus
    {
        Met,
        PartlyMet,
        Missing
    }

    public enum GapPriority
    {
        High,
        Medium
    }

    // One NIS2 measure in the gap report. Change is null for the first report, else "Better", "Same", "Worse" or "New" (R4).
    public sealed record GapLine(string Code, string Question, GapStatus Status, Maturity? Maturity, IReadOnlyList<string> Evidence,
        bool HighRisk, string? Change)
    {
        public bool IsGap => Status != GapStatus.Met;

        // R2: the gaps first by risk, a missing measure before a partly met one
        public GapPriority Priority => Status == GapStatus.Missing || HighRisk ? GapPriority.High : GapPriority.Medium;

        public string Action => Nis2GapReport.ActionFor(Code);
    }

    // US-190: the NIS2 gap report. Pure, so the rules are tested on their own.
    public static class Nis2GapReport
    {
        public const string Kind = "nis2-gap";

        // R1: approved evidence and an answer of 3 or more (or no answer: the evidence was enough to skip the question) is
        // met; evidence with a lower answer, or an answer of 2 or more without evidence, is partly met; the rest is missing
        public static GapStatus Status(bool coveredByEvidence, Maturity? maturity)
            => coveredByEvidence
                ? maturity is null or >= Maturity.Implemented ? GapStatus.Met : GapStatus.PartlyMet
                : maturity >= Maturity.Defined ? GapStatus.PartlyMet : GapStatus.Missing;

        // R4: compared with the last report's statuses (Figures); null when there is none
        public static string? Change(GapStatus now, IReadOnlyDictionary<string, GapStatus>? previous, string code)
            => previous is null ? null
                : !previous.TryGetValue(code, out var before) ? "New"
                : now == before ? "Same"
                : now < before ? "Better" // Met < PartlyMet < Missing
                : "Worse";

        public static IReadOnlyList<GapLine> Lines(IReadOnlyList<GapQuestionView> questions, IReadOnlyDictionary<string, IReadOnlyList<string>> evidence,
            IReadOnlySet<string> highRisk, IReadOnlyDictionary<string, GapStatus>? previous)
            => questions.Select(q =>
            {
                var status = Status(q.CoveredByEvidence, q.Answer?.Maturity);
                return new GapLine(q.Code, q.Question, status, q.Answer?.Maturity,
                    evidence.TryGetValue(q.Code, out var titles) ? titles : [], highRisk.Contains(q.Code), Change(status, previous, q.Code));
            }).ToList();

        // R4: what the next report is compared with, e.g. {"Art. 21(2)(a)":"Missing"}
        public static string Figures(IReadOnlyList<GapLine> lines)
            => JsonSerializer.Serialize(lines.ToDictionary(l => l.Code, l => l.Status.ToString()));

        public static IReadOnlyDictionary<string, GapStatus>? ReadFigures(string? figures)
        {
            if (string.IsNullOrWhiteSpace(figures))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(figures)!
                    .Where(p => Enum.TryParse<GapStatus>(p.Value, out _))
                    .ToDictionary(p => p.Key, p => Enum.Parse<GapStatus>(p.Value), StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // R3: the report for management: the summary with the approval fields, each measure, and the action plan
        public static ReportDocument Document(string organisation, DateOnly today, IReadOnlyList<GapLine> lines,
            IReadOnlyList<SupplierView>? suppliers = null)
        {
            static ReportStatus Colour(GapStatus status) => status switch
            {
                GapStatus.Met => ReportStatus.Good,
                GapStatus.PartlyMet => ReportStatus.Warning,
                _ => ReportStatus.Bad
            };

            var gaps = lines.Where(l => l.IsGap).OrderBy(l => l.Priority).ThenBy(l => l.Code, StringComparer.Ordinal).ToList();
            var summary = new List<(string Label, string Value)>
            {
                ("Organisation", organisation),
                ("Date", today.ToString("dd-MM-yyyy")),
                ("Result", $"{lines.Count(l => l.Status == GapStatus.Met)} met, {lines.Count(l => l.Status == GapStatus.PartlyMet)} partly met, "
                    + $"{lines.Count(l => l.Status == GapStatus.Missing)} missing of {lines.Count} measures"),
                ("How", "Met: approved evidence, and a maturity of 3 or more if answered. Partly met: evidence with a lower maturity, or a maturity of 2 or more without evidence."),
                ("Approved by the management (NIS2 Art. 20(1))", "name ______________________  date ____________  signature ______________________")
            };
            if (lines.Any(l => l.Change is not null))
            {
                summary.Insert(3, ("Since the last report", $"{lines.Count(l => l.Change == "Better")} better, {lines.Count(l => l.Change == "Worse")} worse"));
            }

            return new ReportDocument($"NIS2 gap analysis – {organisation}", summary,
            [
                new ReportSheet("Measures", ["Code", "Measure", "Status", "Maturity", "Evidence", "Since the last report"], 2,
                    lines.Select(l => new ReportRow([l.Code, l.Question, Text(l.Status), l.Maturity is { } m ? Nis2Questions.Title(m) : "Not answered",
                        string.Join("; ", l.Evidence), l.Change ?? ""], Colour(l.Status))).ToList()),
                new ReportSheet("Action plan", ["Priority", "Code", "Action", "Owner", "Due"], 0,
                    gaps.Select(l => new ReportRow([l.Priority.ToString(), l.Code, l.Action, "", ""],
                        l.Priority == GapPriority.High ? ReportStatus.Bad : ReportStatus.Warning)).ToList()),
                // US-191 R5: the supply chain (NIS2 Art. 21(2)(d) and 21(3))
                new ReportSheet("Suppliers", ["Supplier", "Delivers", "Criticality", "Rules", "Status"], 4,
                    (suppliers ?? []).Select(s => new ReportRow([s.Supplier.Name, s.Supplier.Delivers, s.Supplier.Criticality.ToString(),
                        string.Join(", ", s.Supplier.RuleList), $"{s.Status.State}: {s.Status.Detail}"],
                        s.Status.State switch { SupplierState.Ok => ReportStatus.Good, SupplierState.ExpiresSoon => ReportStatus.Warning, _ => ReportStatus.Bad })).ToList())
            ]);
        }

        public static string Text(GapStatus status) => status switch
        {
            GapStatus.PartlyMet => "Partly met",
            _ => status.ToString()
        };

        // R2: a suggested first action per measure
        public static string ActionFor(string code) => code switch
        {
            "Art. 20(1)" => "Have the management approve the security measures and plan how it follows up on them",
            "Art. 20(2)" => "Plan cybersecurity training for the management and offer it to employees",
            "Art. 21(2)(a)" => "Write or update the risk analysis and the information security policy, and set a review date",
            "Art. 21(2)(b)" => "Describe how incidents are detected, reported, handled and learned from",
            "Art. 21(2)(c)" => "Set up tested backups, a disaster recovery plan and a crisis plan",
            "Art. 21(2)(d)" => "List the critical suppliers and add security terms to their agreements",
            "Art. 21(2)(e)" => "Add security to buying and development, including vulnerability handling and disclosure",
            "Art. 21(2)(f)" => "Plan how the measures are tested and measured, e.g. yearly audits or tests",
            "Art. 21(2)(g)" => "Introduce basic cyber hygiene rules and yearly training for all employees",
            "Art. 21(2)(h)" => "Write rules for cryptography and encryption, and check that they are followed",
            "Art. 21(2)(i)" => "Set up access control, an asset list and a process for joiners and leavers",
            "Art. 21(2)(j)" => "Turn on multi-factor authentication and secure the communication channels",
            "Art. 21(3)" => "Assess each critical supplier's vulnerabilities and security practices",
            "Art. 21(4)" => "Describe how a measure that does not work is corrected without delay",
            "Art. 23(1)" => "Describe when and how the authorities and the recipients of the service are notified",
            "Art. 23(4)(a)" => "Prepare templates and contacts for the 24-hour early warning and the 72-hour notification",
            _ => "Decide what is needed to meet the requirement"
        };
    }

    // US-190: makes the report from the evidence (US-188), the answers (US-189) and the risks, and locks it
    public class Nis2GapReportService
    {
        private readonly GapQuestionService _questions;
        private readonly SupplierService _suppliers; // US-191
        private readonly OrganisationAreaService _area;
        private readonly ComplianceQueries _compliance;
        private readonly IReportSnapshotRepository _snapshots;
        private readonly IPdfWriter _pdf;
        private readonly CurrentMember _member;
        private readonly ICurrentUser _currentUser;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public Nis2GapReportService(GapQuestionService questions, SupplierService suppliers, OrganisationAreaService area, ComplianceQueries compliance,
            IReportSnapshotRepository snapshots, IPdfWriter pdf, CurrentMember member, ICurrentUser currentUser, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _questions = questions;
            _suppliers = suppliers;
            _area = area;
            _compliance = compliance;
            _snapshots = snapshots;
            _pdf = pdf;
            _member = member;
            _currentUser = currentUser;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // The lines as they stand now, compared with the last report (for the page)
        public async Task<(Guid AreaId, IReadOnlyList<GapLine> Lines, IReadOnlyList<ReportSnapshot> Reports)> CurrentAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var area = await _area.EnsureAsync(cancellationToken);
            var reports = (await _snapshots.ListAsync(organization.Id, area.Id, cancellationToken)).Where(s => s.Kind == Nis2GapReport.Kind).ToList();

            var compliance = await _compliance.ForProjectAsync(area.Id, cancellationToken);
            var nis2 = (compliance?.Frameworks ?? []).Where(f => f.Framework.ShortName == "NIS2").SelectMany(f => f.Requirements).ToList();
            var evidence = nis2.GroupBy(r => r.Requirement.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.SelectMany(r => r.Evidence.Where(e => !e.IsExpired).Select(e => e.Evidence.Title)
                    .Concat(r.Links.Where(l => !l.Link.NeedsReview).Select(l => l.WorkItem.Title))).Distinct().ToList(),
                StringComparer.OrdinalIgnoreCase);
            var highRisk = nis2.Where(r => r.HighRisk).Select(r => r.Requirement.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var lines = Nis2GapReport.Lines(await _questions.QuestionsAsync(cancellationToken), evidence, highRisk,
                Nis2GapReport.ReadFigures(reports.FirstOrDefault()?.Figures));
            return (area.Id, lines, reports);
        }

        // R3: locked like every report, with its fingerprint; any member may make it, also the Auditor
        public async Task<ReportSnapshot> MakeAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var (areaId, lines, _) = await CurrentAsync(cancellationToken);
            var now = _clock.GetUtcNow().UtcDateTime;
            var today = DateOnly.FromDateTime(now);
            var document = Nis2GapReport.Document(organization.Name, today, lines, await _suppliers.ListAsync(cancellationToken));
            var pdf = _pdf.FromReport(document, new PdfHeader(organization.Name, "Organisation", "NIS2 gap analysis", now, _currentUser.Identity.Name));

            var snapshot = ReportSnapshot.Lock(organization.Id, areaId, Nis2GapReport.Kind, $"NIS2 gap analysis {today:dd-MM-yyyy}",
                $"nis2-gap-analysis-{today:yyyy-MM-dd}.pdf", "application/pdf", pdf, user.Id, user.Role, now, Nis2GapReport.Figures(lines));
            await _snapshots.AddAsync(snapshot, pdf, cancellationToken);
            await _log.LogAsync(ActivityAction.ReportLocked, areaId, $"{snapshot.Title}: SHA-256 {snapshot.Sha256}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return snapshot;
        }
    }
}
