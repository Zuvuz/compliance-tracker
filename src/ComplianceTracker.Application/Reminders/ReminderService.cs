using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Reminders;
using ComplianceTracker.Domain.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Reminders
{
    // US-183: sends mail (implemented in Infrastructure with SMTP, so any mail server works). IsConfigured is false when no
    // server is set up; the tracker then sends nothing and works as before (R4).
    public interface IMailSender
    {
        bool IsConfigured { get; }

        // Throws when the mail cannot be sent; the reminder is tried again
        Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    }

    // US-183: storage for the reminders (implemented in Infrastructure)
    public interface IReminderRepository
    {
        Task<IReadOnlyList<Reminder>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default);
    }

    // One deadline that should give a reminder, and to whom; ProjectName is null for the organisation's own deadlines
    public sealed record DueReminder(ReminderKind Kind, Guid SubjectId, DateTime Due, string? ProjectName, string Path, IReadOnlyList<Guid> To);

    // R2, R5: who gets what. Pure, so the edge cases are tested on their own.
    public static class ReminderPlan
    {
        // R1: within the days before; a deadline that has passed is still reminded once (it was missed, not forgotten)
        public static bool IsDue(DateOnly deadline, DateOnly today, int daysBefore) => today >= deadline.AddDays(-daysBefore);

        // R5: the people with the kind turned on; the reporting deadlines cannot be turned off
        public static IReadOnlyList<Guid> Wanting(IEnumerable<User> people, ReminderKind kind)
            => people.Where(u => u.IsActive && !u.IsErased && (!ReminderKinds.CanTurnOff(kind) || !u.HasTurnedOff(kind))).Select(u => u.Id).Distinct().ToList();

        // R2: the reminders that are new: not made before for the same person, kind, subject and deadline
        public static IReadOnlyList<(Guid UserId, DueReminder Due)> New(IEnumerable<DueReminder> due, IReadOnlyList<Reminder> made)
            => due.SelectMany(d => d.To.Select(user => (user, d)))
                .Where(x => !made.Any(r => r.IsFor(x.user, x.d.Kind, x.d.SubjectId, x.d.Due)))
                .ToList();

        // R3: the mail's text: the kind, the deadline, where and a link; no titles, no evidence and no story text
        public static string Body(string organisation, DueReminder due, string? baseUrl)
            => $"""
                {ReminderKinds.Subject(due.Kind, due.Due)}

                Organisation: {organisation}
                {(due.ProjectName is null ? "Area: the organisation" : $"Project: {due.ProjectName}")}

                Open it in the tracker: {(string.IsNullOrWhiteSpace(baseUrl) ? due.Path : baseUrl.TrimEnd('/') + due.Path)}

                You get this mail because of your role in {organisation}. You can choose which reminders you get on the
                Reminders page in the tracker.
                """;
    }

    public sealed record ReminderRun(int Sent, int Failed);

    // US-183: finds what is due in every organisation and sends the reminders. Run by a background job every hour, so the
    // 24-hour reporting deadlines are not missed; it reads by organisation, not as a signed-in member.
    public class ReminderService
    {
        private readonly IOrganizationRepository _organizations;
        private readonly IProjectRepository _projects;
        private readonly IEvidenceRepository _evidence;
        private readonly IAuditRepository _audits;
        private readonly IApprovalRequestRepository _approvals;
        private readonly ISecurityEventRepository _events;
        private readonly ISupplierRepository _suppliers;
        private readonly IReminderRepository _reminders;
        private readonly IMailSender _mail;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly string? _baseUrl;

        public ReminderService(IOrganizationRepository organizations, IProjectRepository projects, IEvidenceRepository evidence,
            IAuditRepository audits, IApprovalRequestRepository approvals, ISecurityEventRepository events, ISupplierRepository suppliers,
            IReminderRepository reminders, IMailSender mail, IUnitOfWork unitOfWork, TimeProvider clock, ReminderOptions options)
        {
            _organizations = organizations;
            _projects = projects;
            _evidence = evidence;
            _audits = audits;
            _approvals = approvals;
            _events = events;
            _suppliers = suppliers;
            _reminders = reminders;
            _mail = mail;
            _unitOfWork = unitOfWork;
            _clock = clock;
            _baseUrl = options.BaseUrl;
        }

        public async Task<ReminderRun> RunAsync(CancellationToken cancellationToken = default)
        {
            if (!_mail.IsConfigured)
            {
                return new ReminderRun(0, 0); // R4: without a mail server nothing is made or sent
            }

            int sent = 0, failed = 0;
            foreach (var organization in await _organizations.ListAllAsync(cancellationToken))
            {
                var due = await DueAsync(organization, cancellationToken);
                var made = await _reminders.ListAsync(organization.Id, cancellationToken);
                var now = _clock.GetUtcNow().UtcDateTime;
                var waiting = made.Where(r => r.ShouldTry).Select(r => (Reminder: r, Due: due.FirstOrDefault(d => d.Kind == r.Kind && d.SubjectId == r.SubjectId))).ToList();
                foreach (var (userId, d) in ReminderPlan.New(due, made))
                {
                    var reminder = Reminder.Create(organization.Id, userId, d.Kind, d.SubjectId, d.Due, now);
                    await _reminders.AddAsync(reminder, cancellationToken);
                    waiting.Add((reminder, d));
                }

                foreach (var (reminder, d) in waiting)
                {
                    var user = organization.Users.FirstOrDefault(u => u.Id == reminder.UserId);
                    if (user is null || !user.IsActive || d is null)
                    {
                        continue; // left the organisation, or the deadline is gone (e.g. the finding was closed)
                    }

                    try
                    {
                        await _mail.SendAsync(user.Email, ReminderKinds.Subject(reminder.Kind, reminder.Due),
                            ReminderPlan.Body(organization.Name, d, _baseUrl), cancellationToken);
                        reminder.Sent(_clock.GetUtcNow().UtcDateTime);
                        sent++;
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        reminder.Failed(error.Message); // R4: tried again at the next run
                        failed++;
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new ReminderRun(sent, failed);
        }

        // R1: the deadlines of one organisation and who should hear about them
        private async Task<IReadOnlyList<DueReminder>> DueAsync(Organization organization, CancellationToken cancellationToken)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var today = DateOnly.FromDateTime(now);
            var days = organization.ReminderDaysBefore;
            var responsible = organization.Members.Where(u => u.Can(Permission.DecideCompliance)).ToList();
            var due = new List<DueReminder>();
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsClosed).ToList();

            foreach (var project in projects)
            {
                var name = project.IsOrganisationArea ? null : project.Name;
                foreach (var evidence in await _evidence.ListActiveAsync(project.Id, cancellationToken))
                {
                    if (evidence.ValidUntil is { } until && evidence.OrganisationDocumentId is null && ReminderPlan.IsDue(until, today, days) && until >= today)
                    {
                        due.Add(new DueReminder(ReminderKind.EvidenceExpires, evidence.Id, until.ToDateTime(TimeOnly.MinValue), name,
                            $"/Projects/Details/{project.Id}/evidence", ReminderPlan.Wanting(responsible, ReminderKind.EvidenceExpires)));
                    }
                }

                foreach (var securityEvent in (await _events.ListAsync(project.Id, cancellationToken)).Where(e => e.IsOpen && e.IsReportable && !e.RecordedInError))
                {
                    foreach (var report in Enum.GetValues<SecurityReportKind>())
                    {
                        if (securityEvent.SentAt(report) is null && securityEvent.DueAt(report) is { } deadline && deadline > now)
                        {
                            var people = responsible.Concat(organization.Members.Where(u => u.Id == securityEvent.RecordedById));
                            due.Add(new DueReminder(ReminderKind.ReportingDeadline, securityEvent.Id, deadline, name,
                                $"/Projects/Details/{project.Id}/security-events", ReminderPlan.Wanting(people, ReminderKind.ReportingDeadline)));
                        }
                    }
                }
            }

            foreach (var audit in await _audits.ListAsync(projects.Select(p => p.Id).ToList(), cancellationToken))
            {
                foreach (var finding in audit.Findings.Where(f => f.IsOpen && ReminderPlan.IsDue(f.DueOn, today, days)))
                {
                    due.Add(new DueReminder(ReminderKind.FindingDue, finding.Id, finding.DueOn.ToDateTime(TimeOnly.MinValue),
                        projects.FirstOrDefault(p => p.Id == finding.ProjectId) is { IsOrganisationArea: false } p ? p.Name : null,
                        $"/Audits?projectId={finding.ProjectId}",
                        ReminderPlan.Wanting(organization.Members.Where(u => u.Id == finding.OwnerId), ReminderKind.FindingDue)));
                }
            }

            foreach (var request in await _approvals.ListPendingAsync(organization.Id, cancellationToken))
            {
                due.Add(new DueReminder(ReminderKind.ApprovalWaiting, request.Id, request.RequestedAt, null, "/Approvals",
                    ReminderPlan.Wanting(responsible.Where(u => u.Id != request.RequestedById), ReminderKind.ApprovalWaiting)));
            }

            foreach (var supplier in (await _suppliers.ListAsync(organization.Id, cancellationToken)).Where(s => s.IsActive && s.Criticality != SupplierCriticality.Low))
            {
                foreach (var evidence in supplier.Evidence.Where(e => e.ValidUntil is { } until && until >= today && ReminderPlan.IsDue(until, today, days)))
                {
                    due.Add(new DueReminder(ReminderKind.SupplierEvidenceExpires, evidence.Id, evidence.ValidUntil!.Value.ToDateTime(TimeOnly.MinValue), null,
                        "/Suppliers", ReminderPlan.Wanting(responsible, ReminderKind.SupplierEvidenceExpires)));
                }
            }

            return due.Where(d => d.To.Count > 0).ToList();
        }
    }

    // US-183: the address links in mails start with (Reminders:BaseUrl), e.g. "https://tracker.example.com"
    public sealed class ReminderOptions
    {
        public string? BaseUrl { get; set; }
    }

    // US-183 R5: a person's own choices, and the organisation's days before (admins only)
    public class ReminderSettingsService
    {
        private readonly CurrentMember _member;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMailSender _mail;

        public ReminderSettingsService(CurrentMember member, IUnitOfWork unitOfWork, IMailSender mail)
        {
            _member = member;
            _unitOfWork = unitOfWork;
            _mail = mail;
        }

        public bool MailIsConfigured => _mail.IsConfigured;

        public async Task<(User User, int DaysBefore, bool CanSetDays)> MineAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            return (user, organization.ReminderDaysBefore, user.Can(Permission.ManageMembers));
        }

        public async Task SaveAsync(IReadOnlyCollection<ReminderKind> on, int? daysBefore, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            user.SetRemindersOff(Enum.GetValues<ReminderKind>().Where(k => !on.Contains(k)));
            if (daysBefore is { } days && days != organization.ReminderDaysBefore)
            {
                if (!user.Can(Permission.ManageMembers))
                {
                    throw new DomainException("Your role does not allow this");
                }

                organization.SetReminderDaysBefore(days);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
