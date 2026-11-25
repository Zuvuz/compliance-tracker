using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-216 R1, R2: one thing I must do, as a verb, with its deadline and the page where it is done
    public sealed record WeekItem(string Text, DateOnly DueOn, string Page, string? Fragment = null)
    {
        public bool IsOverdue(DateOnly today) => DueOn < today;
    }

    public enum PartState
    {
        Green,
        Yellow,
        Red
    }

    // US-216 R3, R4: one part of the ISMS with its state, what is missing in one sentence and the page that fixes it
    public sealed record IsmsPart(string Clause, string Name, PartState State, string? Missing, string Page);

    // US-216 R5: one step of the guided start
    public sealed record GuideStep(string Next, string Why, bool Done, string Page);

    public sealed record IsmsOverview(
        IReadOnlyList<WeekItem> MyWeek,
        IReadOnlyList<WeekItem> Later,
        IReadOnlyList<IsmsPart> Parts,
        IReadOnlyList<GuideStep> Guide,
        bool GuideHidden,
        bool CanChange,
        DateOnly Today)
    {
        public GuideStep? NextStep => Guide.FirstOrDefault(s => !s.Done);

        public bool ShowsGuide => !GuideHidden && NextStep is not null;
    }

    // The facts the status of the parts is worked out from; pure, so the rules are tested on their own
    public sealed record IsmsFacts(
        IsmsScope? Scope,
        int Policies,
        int PoliciesPastReview,
        int PoliciesReviewSoon,
        int Objectives,
        int ObjectivesToMeasure,
        int ObjectivesOffTarget,
        int MissingCompetences,
        int UnreadPolicies,
        int Tasks,
        int OverdueTasks,
        int TasksDueSoon,
        DateOnly? LastReview,
        bool HasAuditProgramme,
        int OverdueAudits,
        int OverdueCorrectiveActions,
        int OpenNonconformities,
        bool HasIso27001);

    // US-216: the first page of the ISMS area: my week, the status of each part of the ISMS, and the guided start
    public class IsmsOverviewService
    {
        public const int WeekDays = 7;
        public const int SoonDays = 30;

        private readonly IIsmsScopeRepository _scopes;
        private readonly IRecurringTaskRepository _tasks;
        private readonly IManagementReviewRepository _reviews;
        private readonly INonconformityRepository _nonconformities;
        private readonly ISecurityObjectiveRepository _objectives;
        private readonly IAuditProgrammeRepository _programmes;
        private readonly ICompetenceRepository _competences;
        private readonly IAssetRepository _assets;
        private readonly IOrganisationDocumentRepository _documents;
        private readonly IFrameworkRepository _frameworks;
        private readonly IProjectRepository _projects;
        private readonly IRiskRepository _risks;
        private readonly CurrentMember _member;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public IsmsOverviewService(IIsmsScopeRepository scopes, IRecurringTaskRepository tasks, IManagementReviewRepository reviews,
            INonconformityRepository nonconformities, ISecurityObjectiveRepository objectives, IAuditProgrammeRepository programmes,
            ICompetenceRepository competences, IAssetRepository assets, IOrganisationDocumentRepository documents, IFrameworkRepository frameworks,
            IProjectRepository projects, IRiskRepository risks, CurrentMember member, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _scopes = scopes;
            _tasks = tasks;
            _reviews = reviews;
            _nonconformities = nonconformities;
            _objectives = objectives;
            _programmes = programmes;
            _competences = competences;
            _assets = assets;
            _documents = documents;
            _frameworks = frameworks;
            _projects = projects;
            _risks = risks;
            _member = member;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        public async Task<IsmsOverview> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, me) = await _member.RequireAsync(cancellationToken);
            var today = Today;
            var scope = await _scopes.GetAsync(organization.Id, cancellationToken);
            var tasks = (await _tasks.ListAsync(organization.Id, cancellationToken)).Where(t => t.IsActive).ToList();
            var reviews = await _reviews.ListAsync(organization.Id, cancellationToken);
            var nonconformities = await _nonconformities.ListAsync(organization.Id, cancellationToken);
            var objectives = (await _objectives.ListAsync(organization.Id, cancellationToken)).Where(o => o.IsActive).ToList();
            var programme = await _programmes.GetAsync(organization.Id, cancellationToken);
            var documents = (await _documents.ListAsync(organization.Id, cancellationToken: cancellationToken)).Where(d => d.IsActive).ToList();
            var policies = documents.Where(d => d.Kind == OrganisationDocumentKind.Policy).ToList();
            var acknowledgements = await _competences.ListAcknowledgementsAsync(organization.Id, cancellationToken);
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsOrganisationArea && !p.IsClosed).ToList();
            var risks = (await _risks.ListAsync(projects.Select(p => p.Id).ToList(), cancellationToken)).Where(r => !r.IsWithdrawn).ToList();

            // R1, R2: my own items
            var items = new List<WeekItem>();
            items.AddRange(tasks.Where(t => t.OwnerId == me.Id).Select(t => new WeekItem(Verb(t.Title), t.NextDueOn, "YearlyPlan", $"task-{t.Id}")));
            items.AddRange(risks.SelectMany(r => r.Actions.Where(a => a.OwnerId == me.Id && !a.IsDone && a.DueOn is not null)
                .Select(a => new WeekItem($"Carry out the risk action \"{a.Title}\"", a.DueOn!.Value, $"risk:{r.Id}"))));
            items.AddRange(reviews.SelectMany(r => r.Decisions.Where(d => d.IsOpen && d.ActionOwnerId == me.Id)
                .Select(d => new WeekItem($"Carry out \"{d.Text}\" from the management review", d.ActionDueOn!.Value, "ManagementReview"))));
            items.AddRange(nonconformities.Where(n => n.Status == NonconformityStatus.ActionPlanned && n.ActionOwnerId == me.Id && n.ActionDueOn is not null)
                .Select(n => new WeekItem($"Correct \"{n.Title}\": {n.Action}", n.ActionDueOn!.Value, "Nonconformities", $"nonconformity-{n.Id}")));
            items.AddRange(policies.Where(p => !CompetenceService.HasRead(p, me.Id, acknowledgements))
                .Select(p => new WeekItem($"Read the {p.Title}", ReadBy(p), "Competence", "policies-to-read")));
            items.AddRange(documents.Where(d => d.OwnerId == me.Id && d.ReviewDueOn is not null)
                .Select(d => new WeekItem($"Review the {d.Title}", d.ReviewDueOn!.Value, "Documents")));
            items.AddRange(objectives.Where(o => o.IsManual && o.OwnerId == me.Id)
                .Select(o => new WeekItem($"Measure \"{o.Text}\"", o.NextMeasurementOn ?? today, "Objectives", $"objective-{o.Id}")));
            items.AddRange((programme?.Audits ?? []).Where(a => !a.IsDone && a.AuditorId == me.Id)
                .Select(a => new WeekItem($"Hold the internal audit of {string.Join(", ", a.Clauses.Select(c => $"clause {c}").Concat(a.Controls))}", a.On,
                    "InternalAudits", $"planned-{a.Id}")));
            var (week, later) = Week(items, today);

            // R3, R4: the parts
            var members = organization.Members;
            var competences = (await _competences.ListCompetencesAsync(organization.Id, cancellationToken)).Where(c => c.IsActive).ToList();
            var training = await _competences.ListTrainingAsync(organization.Id, cancellationToken);
            var missing = members.Sum(m => competences.Count(c => CompetenceMatrix.StateOf(c, m.Role, training.Where(t => t.UserId == m.Id), today)
                is CompetenceState.Missing or CompetenceState.Expired));
            var unread = members.Count(m => policies.Any(p => !CompetenceService.HasRead(p, m.Id, acknowledgements)));
            var hasIso = (await _frameworks.ListAsync(organization.Id, cancellationToken)).Any(StatementOfApplicability.IsIso27001);
            var facts = new IsmsFacts(scope, policies.Count, policies.Count(p => p.IsReviewDue(today)),
                policies.Count(p => p.ReviewDueOn is { } due && due >= today && due <= today.AddDays(SoonDays)),
                objectives.Count, objectives.Count(o => o.IsManual && o.IsDueForMeasurement(today)), objectives.Count(o => o.State == ObjectiveState.OffTarget),
                missing, unread, tasks.Count, tasks.Count(t => t.IsOverdue(today)), tasks.Count(t => !t.IsOverdue(today) && t.NextDueOn <= today.AddDays(SoonDays)),
                reviews.Where(r => r.HeldOn is not null).Max(r => r.HeldOn), programme is not null, programme?.Audits.Count(a => a.IsOverdue(today)) ?? 0,
                nonconformities.Count(n => n.IsOverdue(today)), nonconformities.Count(n => !n.IsClosed), hasIso);

            // R5: the guided start
            var assets = (await _assets.ListAsync(organization.Id, cancellationToken)).Any(a => a.IsActive);
            var guide = Guide(scope is { Text.Length: > 0 }, policies.Count > 0, assets, risks.Count > 0, hasIso, objectives.Count > 0, tasks.Count > 0);

            return new IsmsOverview(week, later, Parts(facts, today), guide, organization.HidesIsmsGuide,
                await _member.CanAsync(Permission.DecideCompliance, cancellationToken), today);
        }

        // R5: hide or show the guided start for the organisation
        public async Task SetGuideHiddenAsync(bool hidden, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            organization.SetIsmsGuideHidden(hidden);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R1: overdue first, then by deadline, up to 7 days ahead; the rest is "Show more"
        public static (IReadOnlyList<WeekItem> Week, IReadOnlyList<WeekItem> Later) Week(IEnumerable<WeekItem> items, DateOnly today)
        {
            var ordered = items.OrderBy(i => i.DueOn).ThenBy(i => i.Text, StringComparer.OrdinalIgnoreCase).ToList();
            return (ordered.Where(i => i.DueOn <= today.AddDays(WeekDays)).ToList(), ordered.Where(i => i.DueOn > today.AddDays(WeekDays)).ToList());
        }

        // R2: a task's title as something to do
        public static string Verb(string title) => title.Trim().ToLowerInvariant() switch
        {
            "access review" => "Review access rights",
            "backup restore test" => "Test a restore from the backup",
            "review of the policies" => "Review the policies",
            "review of the vulnerability scans" => "Review the vulnerability scans",
            "incident response exercise" => "Hold an incident response exercise",
            "security awareness training" => "Run the security awareness training",
            "supplier review" => "Review the suppliers",
            "review of the risk assessment" => "Review the risk assessment",
            "internal audit" => "Hold the internal audit",
            "management review" => "Hold the management review",
            _ => $"Do the task \"{title.Trim()}\""
        };

        // A policy's current version must be read within 14 days of when it came
        public const int ReadWithinDays = 14;

        public static DateOnly ReadBy(OrganisationDocument policy)
        {
            var since = policy.Versions.Select(v => v.ReplacedAt).DefaultIfEmpty(policy.AddedAt).Max();
            return DateOnly.FromDateTime(since).AddDays(ReadWithinDays);
        }

        // R3, R4: red when something required is missing or overdue, yellow when something is due within 30 days
        public static IReadOnlyList<IsmsPart> Parts(IsmsFacts f, DateOnly today)
        {
            IsmsPart Part(string clause, string name, string page, params (PartState State, string Text)?[] checks)
            {
                var worst = checks.OfType<(PartState State, string Text)>().OrderByDescending(c => c.State).FirstOrDefault();
                return worst == default ? new IsmsPart(clause, name, PartState.Green, null, page) : new IsmsPart(clause, name, worst.State, worst.Text, page);
            }

            (PartState, string)? Red(bool when, string text) => when ? (PartState.Red, text) : null;
            (PartState, string)? Yellow(bool when, string text) => when ? (PartState.Yellow, text) : null;
            static string N(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

            var scope = f.Scope;
            var monthsSinceReview = f.LastReview is { } last ? Months(last, today) : (int?)null;
            return
            [
                Part("4", "Context", "Scope",
                    Red(scope is null || scope.Text.Length == 0, "The ISMS scope is not described"),
                    Red(scope is { Text.Length: > 0, Version: 0 }, "The ISMS scope is not approved"),
                    Red(scope?.IsReviewOverdue(today) == true, "The review of the ISMS scope is overdue"),
                    Yellow(scope is { Version: > 0, ChangedSinceApproval: true }, "The ISMS scope has changed since it was approved"),
                    Yellow(scope?.NextReviewOn is { } due && due >= today && due <= today.AddDays(SoonDays), "The ISMS scope is due for review within 30 days")),
                Part("5", "Leadership", "Documents",
                    Red(f.Policies == 0, "No policies yet"),
                    Red(f.PoliciesPastReview > 0, $"{N(f.PoliciesPastReview, "policy is", "policies are")} past the review date"),
                    Yellow(f.PoliciesReviewSoon > 0, $"{N(f.PoliciesReviewSoon, "policy is", "policies are")} due for review within 30 days")),
                Part("6", "Planning", "Objectives",
                    Red(f.Objectives == 0, "No security objectives yet"),
                    Red(f.ObjectivesToMeasure > 0, $"{N(f.ObjectivesToMeasure, "objective is", "objectives are")} due for a measurement"),
                    Yellow(f.ObjectivesOffTarget > 0, $"{N(f.ObjectivesOffTarget, "objective is", "objectives are")} off target")),
                Part("7", "Support", "Competence",
                    Red(f.MissingCompetences > 0, $"{N(f.MissingCompetences, "required competence is", "required competences are")} missing or expired"),
                    Yellow(f.UnreadPolicies > 0, $"{N(f.UnreadPolicies, "member has", "members have")} not read the current policies")),
                Part("8", "Operation", "YearlyPlan",
                    Red(f.Tasks == 0, "No tasks in the yearly plan"),
                    Red(f.OverdueTasks > 0, $"{N(f.OverdueTasks, "task is", "tasks are")} overdue"),
                    Yellow(f.TasksDueSoon > 0, $"{N(f.TasksDueSoon, "task is", "tasks are")} due within 30 days")),
                Part("9", "Performance evaluation", "ManagementReview",
                    Red(monthsSinceReview is null, "No management review yet"),
                    Red(monthsSinceReview >= ManagementReview.IntervalMonths, $"No management review for {monthsSinceReview} months"),
                    Red(!f.HasAuditProgramme, "No internal audit programme"),
                    Red(f.OverdueAudits > 0, $"{N(f.OverdueAudits, "planned internal audit is", "planned internal audits are")} overdue"),
                    Yellow(monthsSinceReview == ManagementReview.IntervalMonths - 1, "The management review is due within a month")),
                Part("10", "Improvement", "Nonconformities",
                    Red(f.OverdueCorrectiveActions > 0, $"{N(f.OverdueCorrectiveActions, "corrective action is", "corrective actions are")} overdue"),
                    Yellow(f.OpenNonconformities > 0, $"{N(f.OpenNonconformities, "nonconformity is", "nonconformities are")} open")),
                Part("6.1.3", "Statement of Applicability", "Frameworks",
                    Yellow(!f.HasIso27001, "No ISO/IEC 27001 framework, so there is no Statement of Applicability"))
            ];
        }

        // Whole months between two days, e.g. 2025-09-01 to 2026-11-21 is 14
        public static int Months(DateOnly from, DateOnly to)
        {
            var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
            return to.Day < from.Day ? months - 1 : months;
        }

        // R5: the order of the guided start
        public static IReadOnlyList<GuideStep> Guide(bool scope, bool policies, bool assets, bool risks, bool soa, bool objectives, bool plan) =>
        [
            new("Next: describe your ISMS scope", "The scope says what the ISMS covers, and everything else builds on it.", scope, "Scope"),
            new("Next: add your policies", "The policies say what the organisation has decided, and everyone must read them.", policies, "Documents"),
            new("Next: list your assets", "You can only protect what you know you have.", assets, "Assets"),
            new("Next: assess your risks", "The risks decide which controls you need.", risks, "Projects"),
            new("Next: add ISO/IEC 27001 for your Statement of Applicability", "The SoA says which controls apply and how they are met.", soa, "Frameworks"),
            new("Next: set your security objectives", "Objectives show whether the ISMS works.", objectives, "Objectives"),
            new("Next: plan the year", "The yearly plan makes sure the recurring work gets done.", plan, "YearlyPlan")
        ];
    }
}
