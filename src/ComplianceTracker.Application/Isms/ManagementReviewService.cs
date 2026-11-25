using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-210: storage for the management reviews (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IManagementReviewRepository
    {
        // The organisation's reviews with their decisions
        Task<IReadOnlyList<ManagementReview>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(ManagementReview review, CancellationToken cancellationToken = default);
    }

    // What the tracker gathered for the input pack, each part as lines; empty means nothing to report
    public sealed record ReviewFacts(
        IReadOnlyList<string> OpenActions,
        IReadOnlyList<string> Context,
        IReadOnlyList<string> PartyNeeds,
        IReadOnlyList<string> Objectives,
        IReadOnlyList<string> Nonconformities,
        IReadOnlyList<string> Audits,
        string? Feedback,
        IReadOnlyList<string> Risks,
        string? Improvements);

    public sealed record ManagementReviewView(
        IReadOnlyList<ManagementReview> Reviews,
        ManagementReview? Current,
        IReadOnlyList<ReviewInput> Inputs,
        IReadOnlyDictionary<Guid, string> Members,
        bool ReviewIsDue,
        bool CanChange,
        DateOnly Today)
    {
        public string NameOf(Guid? id) => id is { } known ? Members.GetValueOrDefault(known) ?? "Former member" : "";
    }

    public class ManagementReviewService
    {
        public const string Title = "Management review";
        public const string Kind = "management-review";

        private readonly IManagementReviewRepository _reviews;
        private readonly IIsmsScopeRepository _scopes;
        private readonly ISecurityObjectiveRepository _objectives;
        private readonly INonconformityRepository _nonconformities;
        private readonly IAuditProgrammeRepository _programmes;
        private readonly IAuditRepository _audits;
        private readonly IRiskRepository _risks;
        private readonly IProjectRepository _projects;
        private readonly CurrentMember _member;
        private readonly ICurrentUser _currentUser;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly IPdfWriter _pdf;
        private readonly OrganisationAreaService _area;
        private readonly ReportService _reports;

        public ManagementReviewService(IManagementReviewRepository reviews, IIsmsScopeRepository scopes, ISecurityObjectiveRepository objectives,
            INonconformityRepository nonconformities, IAuditProgrammeRepository programmes, IAuditRepository audits, IRiskRepository risks,
            IProjectRepository projects, CurrentMember member, ICurrentUser currentUser, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock,
            IPdfWriter pdf, OrganisationAreaService area, ReportService reports)
        {
            _reviews = reviews;
            _scopes = scopes;
            _objectives = objectives;
            _nonconformities = nonconformities;
            _programmes = programmes;
            _audits = audits;
            _risks = risks;
            _projects = projects;
            _member = member;
            _currentUser = currentUser;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
            _pdf = pdf;
            _area = area;
            _reports = reports;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // Every member reads the reviews. The current one is the newest; while it is planned, the input pack is gathered now.
        public async Task<ManagementReviewView> ViewAsync(Guid? reviewId = null, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var reviews = (await _reviews.ListAsync(organization.Id, cancellationToken)).OrderByDescending(r => r.PlannedOn).ToList();
            var current = reviewId is { } id ? reviews.FirstOrDefault(r => r.Id == id) : reviews.FirstOrDefault();
            var inputs = current is null ? [] : current.Status == ReviewStatus.Planned ? await GatherAsync(organization, current, reviews, cancellationToken) : current.Inputs;
            return new ManagementReviewView(reviews, current, inputs, organization.Users.ToDictionary(u => u.Id, u => u.Name),
                ManagementReview.ReviewIsDue(reviews, Today), await _member.CanAsync(Permission.DecideCompliance, cancellationToken), Today);
        }

        // R1
        public async Task<ManagementReview> PlanAsync(DateOnly? plannedOn, IReadOnlyList<string?>? participants, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            if ((await _reviews.ListAsync(organization.Id, cancellationToken)).Any(r => r.Status == ReviewStatus.Planned))
            {
                throw new DomainException("A review is planned already; hold it or change its date");
            }

            var review = ManagementReview.Plan(organization.Id, plannedOn, participants, Now);
            await _reviews.AddAsync(review, cancellationToken);
            await _log.LogAsync(ActivityAction.ManagementReviewChanged, organization.Id,
                $"Planned the management review on {review.PlannedOn:dd-MM-yyyy} with {string.Join(", ", review.Participants)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return review;
        }

        public Task ChangeAsync(Guid reviewId, DateOnly? plannedOn, IReadOnlyList<string?>? participants, string? feedback, string? improvements,
            CancellationToken cancellationToken = default)
            => WithReviewAsync(reviewId, (_, review) =>
            {
                review.Change(plannedOn, participants);
                review.Describe(feedback, improvements);
                return Task.FromResult($"Changed the management review of {review.PlannedOn:dd-MM-yyyy}");
            }, cancellationToken);

        // R2, R3
        public Task HoldAsync(Guid reviewId, DateOnly? heldOn, CancellationToken cancellationToken = default)
            => WithReviewAsync(reviewId, async (organization, review) =>
            {
                var all = await _reviews.ListAsync(organization.Id, cancellationToken);
                review.Hold(heldOn, await GatherAsync(organization, review, all, cancellationToken), Today);
                return $"Held the management review on {review.HeldOn:dd-MM-yyyy}; the inputs are kept";
            }, cancellationToken);

        // R4
        public async Task DecideAsync(Guid reviewId, string? text, Guid? actionOwnerId, DateOnly? actionDueOn, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            if (actionOwnerId is { } owner && organization.Members.All(m => m.Id != owner))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }

            await WithReviewAsync(reviewId, (_, review) =>
            {
                var decision = review.Decide(text, actionOwnerId, actionDueOn, Now);
                return Task.FromResult($"Decision: {decision.Text}"
                    + (decision.HasAction ? $"; action for {organization.Users.First(u => u.Id == decision.ActionOwnerId).Name} by {decision.ActionDueOn:dd-MM-yyyy}" : ""));
            }, cancellationToken);
        }

        // R5: the minutes are locked, and the PDF is locked with a fingerprint on the organisation area's Reports tab (US-050)
        public async Task LockAsync(Guid reviewId, CancellationToken cancellationToken = default)
        {
            await WithReviewAsync(reviewId, (_, review) =>
            {
                review.Lock(Now);
                return Task.FromResult($"Locked the minutes of the management review held on {review.HeldOn:dd-MM-yyyy}");
            }, cancellationToken);
            var area = await _area.EnsureAsync(cancellationToken);
            await _reports.LockFileAsync(area.Id, await PdfAsync(reviewId, cancellationToken), cancellationToken);
        }

        // R5: an action can be done after the minutes are locked
        public Task MarkActionDoneAsync(Guid reviewId, Guid decisionId, CancellationToken cancellationToken = default)
            => WithReviewAsync(reviewId, (_, review) =>
            {
                var decision = review.MarkActionDone(decisionId, Now);
                return Task.FromResult($"Action done: {decision.Text}");
            }, cancellationToken);

        public async Task<ReportFile> PdfAsync(Guid reviewId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var view = await ViewAsync(reviewId, cancellationToken);
            var review = view.Current ?? throw new DomainException("Review not found");
            var now = Now;
            var document = Minutes(view, review, organization.Name, now);
            var header = new PdfHeader(organization.Name, "Organisation", Title, now, _currentUser.Identity.Name);
            return new ReportFile(Kind, document.Title,
                $"management-review-{Frameworks.FrameworkSlugs.Slug(organization.Name)}-{(review.HeldOn ?? review.PlannedOn):yyyy-MM-dd}.pdf", "application/pdf",
                _pdf.FromReport(document, header));
        }

        // R2: every input of 9.3.2, "Nothing to report" when a part is empty
        public static IReadOnlyList<ReviewInput> Compose(ReviewFacts facts)
        {
            static IReadOnlyList<string> Or(IReadOnlyList<string> lines) => lines.Count == 0 ? [ReviewInput.NothingToReport] : lines;
            static IReadOnlyList<string> Text(string? text)
                => string.IsNullOrWhiteSpace(text) ? [ReviewInput.NothingToReport] : text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

            return
            [
                new("9.3.2 a", "Actions from the last review", Or(facts.OpenActions)),
                new("9.3.2 b", "Changes in the context", Or(facts.Context)),
                new("9.3.2 c", "Needs of the interested parties", Or(facts.PartyNeeds)),
                new("9.3.2 d", "Objectives", Or(facts.Objectives)),
                new("9.3.2 d", "Nonconformities", Or(facts.Nonconformities)),
                new("9.3.2 d", "Audit results", Or(facts.Audits)),
                new("9.3.2 e", "Feedback from interested parties", Text(facts.Feedback)),
                new("9.3.2 f", "Risks", Or(facts.Risks)),
                new("9.3.2 g", "Opportunities for improvement", Text(facts.Improvements))
            ];
        }

        // The minutes as a report: the inputs, the decisions and the actions
        public static ReportDocument Minutes(ManagementReviewView view, ManagementReview review, string organisation, DateTime now)
            => new($"{Title} – {organisation}",
            [
                ("Planned", review.PlannedOn.ToString("dd-MM-yyyy")),
                ("Held", review.HeldOn?.ToString("dd-MM-yyyy") ?? "Not yet"),
                ("Participants", string.Join(", ", review.Participants)),
                ("Status", review.Status.ToString()),
                ("Made", $"{now.ToLocalTime():dd-MM-yyyy HH:mm}")
            ],
            [
                new ReportSheet("Inputs", ["Clause", "Input", "What the tracker found"], -1,
                    view.Inputs.SelectMany(i => i.Lines.Select(l => new ReportRow([i.Clause, i.Title, l],
                        l == ReviewInput.NothingToReport ? ReportStatus.Muted : ReportStatus.Normal))).ToList()),
                new ReportSheet("Decisions and actions", ["Decision", "Action owner", "Deadline", "Done"], 3,
                    review.Decisions.Select(d => new ReportRow([d.Text, view.NameOf(d.ActionOwnerId), d.ActionDueOn?.ToString("dd-MM-yyyy") ?? "",
                        d.HasAction ? d.ActionDoneAt is { } done ? done.ToLocalTime().ToString("dd-MM-yyyy") : "Open" : ""],
                        d.IsOpen ? ReportStatus.Warning : ReportStatus.Normal)).ToList())
            ]);

        // R2: gathered from the ISMS: the earlier reviews' open actions, the scope and parties, objectives, nonconformities,
        // audits, and the risks of the projects inside the scope
        private async Task<IReadOnlyList<ReviewInput>> GatherAsync(Organization organization, ManagementReview review, IReadOnlyList<ManagementReview> all,
            CancellationToken cancellationToken)
        {
            var today = Today;
            var yearAgo = today.AddYears(-1);
            string Name(Guid? id) => organization.Users.FirstOrDefault(u => u.Id == id)?.Name ?? "";

            var openActions = all.Where(r => r.Id != review.Id).SelectMany(r => r.Decisions).Where(d => d.IsOpen)
                .Select(d => $"{d.Text}: {Name(d.ActionOwnerId)}, due {d.ActionDueOn:dd-MM-yyyy}{(d.ActionDueOn < today ? " (overdue)" : "")}").ToList();

            var scope = await _scopes.GetAsync(organization.Id, cancellationToken);
            var parties = (await _scopes.ListPartiesAsync(organization.Id, cancellationToken)).Where(p => p.IsActive).ToList();
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsOrganisationArea && !p.IsClosed).ToList();
            var inside = projects.Where(p => scope?.IsInside(p.Id) == true).ToList();
            var context = new List<string>();
            if (scope is not null)
            {
                context.Add(scope.Version == 0 ? "The ISMS scope is not approved yet" : $"ISMS scope version {scope.Version}, approved {scope.ApprovedAt!.Value.ToLocalTime():dd-MM-yyyy}");
                if (scope is { Version: > 0, ChangedSinceApproval: true })
                {
                    context.Add($"The scope has changed since it was approved ({scope.ChangedAt.ToLocalTime():dd-MM-yyyy})");
                }

                context.Add($"{inside.Count} of {projects.Count} projects inside the scope");
            }

            if (parties.Count > 0)
            {
                context.Add($"{parties.Count} interested part{(parties.Count == 1 ? "y" : "ies")}");
            }

            var needs = parties.SelectMany(p => p.Requirements.Where(r => !r.Handled).Select(r => $"{p.Name}: {r.Text} is not handled")).ToList();

            var objectives = (await _objectives.ListAsync(organization.Id, cancellationToken)).Where(o => o.IsActive)
                .Select(o => o.Last is { } last
                    ? $"{o.Text}: {ObjectiveService.Format(last.Value, o.Unit)} against {ObjectiveService.Target(o)} ({o.State})"
                    : $"{o.Text}: not measured yet")
                .ToList();

            var nonconformities = await _nonconformities.ListAsync(organization.Id, cancellationToken);
            var nonconformityLines = new List<string>();
            var open = nonconformities.Count(n => !n.IsClosed);
            var closed = nonconformities.Count(n => n.ClosedAt is { } c && DateOnly.FromDateTime(c) >= yearAgo);
            if (open > 0 || closed > 0)
            {
                nonconformityLines.Add($"{open} open, {closed} closed in the last 12 months");
            }

            nonconformityLines.AddRange(nonconformities.Where(n => n.IsOverdue(today)).Select(n => $"{n.Title}: the corrective action is overdue"));
            nonconformityLines.AddRange(nonconformities.Where(n => n.FailedActions.Count > 0 && !n.IsClosed).Select(n => $"{n.Title}: an action did not work"));

            var auditLines = new List<string>();
            if (await _programmes.GetAsync(organization.Id, cancellationToken) is { } programme)
            {
                var done = programme.Audits.Count(a => a.IsDone);
                var overdue = programme.Audits.Count(a => a.IsOverdue(today));
                auditLines.Add($"Internal audit programme: {done} of {programme.Audits.Count} planned audits done{(overdue > 0 ? $", {overdue} overdue" : "")}");
            }

            var names = projects.ToDictionary(p => p.Id, p => p.Name);
            foreach (var audit in (await _audits.ListAsync(projects.Select(p => p.Id).ToList(), cancellationToken)).Where(a => a.HeldOn >= yearAgo).OrderBy(a => a.HeldOn))
            {
                auditLines.Add($"{audit.HeldOn:dd-MM-yyyy} {audit.Kind} audit of {names.GetValueOrDefault(audit.ProjectId, "")}: {audit.Result}, "
                               + $"{audit.Findings.Count(f => f.IsOpen)} open finding{(audit.Findings.Count(f => f.IsOpen) == 1 ? "" : "s")}");
            }

            var riskLines = new List<string>();
            foreach (var project in inside)
            {
                foreach (var risk in (await _risks.ListAsync(project.Id, cancellationToken))
                             .Where(r => !r.IsWithdrawn && r.Status != RiskStatus.Closed && r.Level >= RiskLevel.High).OrderByDescending(r => r.Score))
                {
                    riskLines.Add($"{risk.Title} ({risk.Level}, {project.Name}){(risk.Status == RiskStatus.Accepted ? ", accepted" : "")}");
                }
            }

            return Compose(new ReviewFacts(openActions, context, needs, objectives, nonconformityLines, auditLines, review.Feedback, riskLines, review.Improvements));
        }

        private async Task WithReviewAsync(Guid reviewId, Func<Organization, ManagementReview, Task<string>> change, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var review = (await _reviews.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(r => r.Id == reviewId)
                         ?? throw new DomainException("Review not found");
            var detail = await change(organization, review);
            await _log.LogAsync(ActivityAction.ManagementReviewChanged, organization.Id, detail, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
