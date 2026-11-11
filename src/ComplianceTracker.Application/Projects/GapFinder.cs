using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Projects
{
    // US-119 R1: the four categories, in the order the tab shows them
    public enum GapCategory
    {
        Requirements,
        Security,
        Risk,
        Scanning
    }

    // US-119 R2: what to do about a gap; the page turns each into a button (R4)
    public enum GapAction
    {
        AskTheAi,      // US-106: matching stories and evidence, or a draft story (the draft work of US-108)
        AddRisk,       // a filled-in risk form
        AddAction,     // the risk's page, to add a treatment action
        Open           // the source's own page
    }

    // US-119 R2: one gap: its category, what is missing, the suggested action, its source and where it lives. Key and Stamp
    // identify the gap and its source's state, so a dismissal holds until the source changes (R5).
    public record Gap(
        GapCategory Category, string Key, string Stamp, string Title, string Missing, string Suggestion, GapAction Action,
        string Source, Guid? RequirementId = null, Guid? RiskId = null, string? Tab = null, string? Sub = null);

    // US-119: the gaps of one project, per category, and what each category needs before it can find gaps (R6)
    public record GapsView(IReadOnlyDictionary<GapCategory, IReadOnlyList<Gap>> Gaps, IReadOnlyDictionary<GapCategory, string> Empty,
        int StorySuggestions, int EvidenceSuggestions, int Dismissed)
    {
        public IReadOnlyList<Gap> Of(GapCategory category) => Gaps.TryGetValue(category, out var gaps) ? gaps : [];
    }

    // Storage for dismissed gaps (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IGapDismissalRepository
    {
        Task<IReadOnlyList<GapDismissal>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(GapDismissal dismissal, CancellationToken cancellationToken = default);
    }

    // US-119: finds the project's gaps with plain checks, no AI (R3). The AI's own suggestions stay where they are and are
    // counted here with a link (open question 2). A gap only becomes work when a person acts on it (R4).
    public class GapFinder
    {
        private readonly ComplianceQueries _compliance;
        private readonly Risks.IRiskRepository _risks;
        private readonly Scanning.IScanRepository _scans;
        private readonly Security.ISecurityEventRepository _events;
        private readonly IGapDismissalRepository _dismissals;
        private readonly IProjectRepository _projects;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public GapFinder(
            ComplianceQueries compliance, Risks.IRiskRepository risks, Scanning.IScanRepository scans, Security.ISecurityEventRepository events,
            IGapDismissalRepository dismissals, IProjectRepository projects, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _compliance = compliance;
            _risks = risks;
            _scans = scans;
            _events = events;
            _dismissals = dismissals;
            _projects = projects;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // Every member may look (the Auditor too); null for other organisations' projects
        public async Task<GapsView?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project
                || await _compliance.ForProjectAsync(projectId, cancellationToken) is not { } compliance)
            {
                return null;
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            var gaps = new List<Gap>();
            var empty = new Dictionary<GapCategory, string>();

            gaps.AddRange(RequirementGaps(compliance));
            if (compliance.Frameworks.Count == 0)
            {
                empty[GapCategory.Requirements] = "Add a framework to the project to find gaps in its requirements";
            }

            var risks = (await _risks.ListAsync(project.Id, cancellationToken)).Where(r => r.Status != RiskStatus.Closed).ToList();
            gaps.AddRange(RiskGaps(risks, DateOnly.FromDateTime(now)));
            if (risks.Count == 0)
            {
                empty[GapCategory.Risk] = "Add the project's risks to find gaps in their treatment (Risks tab)";
            }

            var findings = await _scans.ListFindingsAsync(project.Id, cancellationToken);
            gaps.AddRange(ScanningGaps(findings));
            if ((await _scans.ListAsync(project.Id, cancellationToken)).Count == 0)
            {
                empty[GapCategory.Scanning] = "Import scan results to find gaps here (US-078); an SBOM can be imported later (US-079)";
            }

            var events = await _events.ListAsync(project.Id, cancellationToken);
            gaps.AddRange(SecurityGaps(events, now));
            if (events.Count == 0)
            {
                empty[GapCategory.Security] = "No security events are recorded, so no reporting deadline can be missed";
            }

            // R5: a dismissed gap stays away while its source is unchanged
            var dismissals = await _dismissals.ListAsync(project.Id, cancellationToken);
            var shown = gaps.Where(g => !dismissals.Any(d => d.Hides(g.Key, g.Stamp))).ToList();

            return new GapsView(
                shown.GroupBy(g => g.Category).ToDictionary(g => g.Key, g => (IReadOnlyList<Gap>)g.ToList()),
                empty,
                compliance.Frameworks.Sum(f => f.SuggestionCount),
                compliance.Frameworks.Sum(f => f.Requirements.Sum(r => r.SuggestedEvidence.Count)),
                gaps.Count - shown.Count);
        }

        // R5, R7: dismissing needs a reason and LinkWork; the gap is found again first, so its stamp is the current one
        public async Task DismissAsync(Guid projectId, string key, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            var gap = (await ForProjectAsync(project.Id, cancellationToken))?.Gaps.Values.SelectMany(g => g).FirstOrDefault(g => g.Key == key)
                ?? throw new DomainException("The gap was not found");

            await _dismissals.AddAsync(GapDismissal.Create(project.Id, gap.Key, gap.Stamp, reason, user.Id, _clock.GetUtcNow().UtcDateTime, user.Role), cancellationToken);
            await _log.LogAsync(ActivityAction.GapDismissed, project.Id, $"{gap.Category}: {gap.Title} ({reason?.Trim()})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // ---------------------------------------------------------- The checks (R3: no AI)

        // Requirements: applicable requirements (not recommendations) that are not covered, or covered without evidence
        public static IEnumerable<Gap> RequirementGaps(ProjectCompliance compliance)
            => compliance.Frameworks.SelectMany(framework => framework.Requirements
                .Where(r => r.IsApplicable && !r.Requirement.IsRecommendation)
                .Select(r =>
                {
                    var name = $"{Frameworks.FrameworkSlugs.Short(framework.Framework.Name)} {r.Requirement.Code}";
                    var sub = Frameworks.FrameworkSlugs.Slug(framework.Framework.Name);
                    return !r.IsCovered
                        ? new Gap(GapCategory.Requirements, $"requirement:{r.Requirement.Id}:not-covered", "not-covered",
                            $"{r.Requirement.Code} {r.Requirement.Title}", "Not covered by any user story",
                            "Ask the AI for matching stories and evidence, or a draft story", GapAction.AskTheAi, name, r.Requirement.Id, Tab: "compliance", Sub: sub)
                        : !r.HasEvidence
                            ? new Gap(GapCategory.Requirements, $"requirement:{r.Requirement.Id}:no-evidence", "no-evidence",
                                $"{r.Requirement.Code} {r.Requirement.Title}", "Covered, but no evidence proves it",
                                "Add evidence or ask the AI for matching evidence", GapAction.AskTheAi, name, r.Requirement.Id, Tab: "compliance", Sub: sub)
                            : null;
                })
                .OfType<Gap>());

        // Risk: open risks to reduce without treatment actions (US-111), overdue actions, and treated risks without a residual level
        public static IEnumerable<Gap> RiskGaps(IEnumerable<Risk> risks, DateOnly today)
        {
            foreach (var risk in risks.Where(r => r.Status != RiskStatus.Closed).OrderByDescending(r => r.Score))
            {
                if (risk.Treatment == RiskTreatment.Reduce && risk.Actions.Count == 0)
                {
                    yield return new Gap(GapCategory.Risk, $"risk:{risk.Id}:no-actions", $"actions:{risk.Actions.Count}",
                        risk.Title, $"No treatment actions ({risk.Level})", "Add a treatment action", GapAction.AddAction, "Risk register", RiskId: risk.Id);
                }

                if (risk.OverdueActions(today) is var overdue and > 0)
                {
                    yield return new Gap(GapCategory.Risk, $"risk:{risk.Id}:overdue", $"overdue:{overdue}",
                        risk.Title, $"{overdue} overdue {(overdue == 1 ? "action" : "actions")}", "Finish or reschedule the actions", GapAction.AddAction, "Risk register", RiskId: risk.Id);
                }

                if (risk.Actions.Count > 0 && risk.Actions.All(a => a.Status == RiskActionStatus.Done) && risk.ResidualLevel is null)
                {
                    yield return new Gap(GapCategory.Risk, $"risk:{risk.Id}:no-residual", "no-residual",
                        risk.Title, "Every action is done, but the residual level is not set", "Set the residual likelihood and impact", GapAction.Open, "Risk register", RiskId: risk.Id);
                }
            }
        }

        // Scanning: open high and critical findings (US-078)
        public static IEnumerable<Gap> ScanningGaps(IEnumerable<Finding> findings)
            => findings.Where(f => f.State == FindingState.Open && f.Severity >= FindingSeverity.High)
                .OrderByDescending(f => f.Severity)
                .Select(f => new Gap(GapCategory.Scanning, $"finding:{f.Id}", $"{f.State}:{f.LastScanId}",
                    Title(f), $"{f.Severity} finding from {f.Tool}{(f.Location is null ? "" : " in " + f.Location)} with no fix yet",
                    "Add a risk for it, or fix it and scan again", GapAction.AddRisk, f.Tool, Tab: "scans"));

        // "SQL injection in /search – A vulnerable package": the scanner's rule, then its message
        private static string Title(Finding finding)
        {
            var title = string.IsNullOrWhiteSpace(finding.Message) ? finding.RuleId : $"{finding.RuleId} – {finding.Message}";
            return title.Length <= 200 ? title : title[..200];
        }

        // Security: open security events whose report is due or past due (US-073)
        public static IEnumerable<Gap> SecurityGaps(IEnumerable<SecurityEvent> events, DateTime now)
        {
            foreach (var securityEvent in events.Where(e => e.IsOpen))
            {
                foreach (var report in Enum.GetValues<SecurityReportKind>())
                {
                    if (securityEvent.SentAt(report) is null && securityEvent.DueAt(report) is { } due && due <= now)
                    {
                        var name = report switch
                        {
                            SecurityReportKind.EarlyWarning => "early warning",
                            SecurityReportKind.Notification => "notification",
                            _ => "final report"
                        };
                        yield return new Gap(GapCategory.Security, $"event:{securityEvent.Id}:{report}", "unsent",
                            securityEvent.Title, $"The {name} was due {due.ToLocalTime():dd-MM-yyyy HH:mm}", $"Send the {name} (CRA Art. 14)", GapAction.Open,
                            "Security events", Tab: "security-events");
                    }
                }
            }
        }
    }
}
