using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Security
{
    // US-073: an actively exploited vulnerability or a severe incident of a product on the market (CRA Art. 14), with
    // its three reports and their deadlines. The tracker does not send the reports (R4): a person marks each as sent,
    // with the time and a reference. Its own aggregate; never deleted, closed when every report is sent.
    public class SecurityEvent
    {
        public const int MaxTitleLength = 200;
        public const int MaxDescriptionLength = 4000;
        public const int MaxReferenceLength = 200;

        // R2, Art. 14(2) and (4): counted from when the manufacturer became aware (checked against the text, B49)
        public static readonly TimeSpan EarlyWarningWithin = TimeSpan.FromHours(24);
        public static readonly TimeSpan NotificationWithin = TimeSpan.FromHours(72);
        public static readonly TimeSpan VulnerabilityFinalReportAfterFix = TimeSpan.FromDays(14);
        public const int IncidentFinalReportMonthsAfterNotification = 1;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public SecurityEventKind Kind { get; private set; }

        // e.g. "CVE-2026-1234" or "Ransomware on the update server"
        public string Title { get; private set; } = "";
        public string? Description { get; private set; }

        // When the manufacturer became aware of it (UTC); the deadlines count from here
        public DateTime AwareAt { get; private set; }

        public Guid RecordedById { get; private set; }
        public UserRole? RecordedRole { get; private set; }
        public DateTime RecordedAt { get; private set; }

        // Art. 14(2)(c): the final report on a vulnerability is due 14 days after a corrective or mitigating measure is available
        public DateTime? FixAvailableAt { get; private set; }

        public DateTime? EarlyWarningSentAt { get; private set; }
        public string? EarlyWarningReference { get; private set; }
        public DateTime? NotificationSentAt { get; private set; }
        public string? NotificationReference { get; private set; }
        public DateTime? FinalReportSentAt { get; private set; }
        public string? FinalReportReference { get; private set; }

        public DateTime? ClosedAt { get; private set; }
        public Guid? ClosedById { get; private set; }

        // US-127 R5: why a vulnerability that is not exploited is closed without being disclosed
        public string? ClosedReason { get; private set; }

        // US-127: a vulnerability that is not exploited is disclosed after the fix (CRA Annex I, Part II (4))
        public DateTime? DisclosedAt { get; private set; }

        // US-127 R2, R3: upgraded once to a reportable kind; AwareAt then holds when it became reportable, and the
        // first awareness is kept here
        public DateTime? UpgradedAt { get; private set; }
        public Guid? UpgradedById { get; private set; }
        public string? UpgradeReason { get; private set; }
        public DateTime? FirstAwareAt { get; private set; }

        public bool IsOpen => ClosedAt is null;

        // US-127 R1: only these two kinds have reports and deadlines (CRA Art. 14(1), (3))
        public bool IsReportable => Kind is SecurityEventKind.ActivelyExploitedVulnerability or SecurityEventKind.SevereIncident;

        public bool IsVulnerability => Kind is SecurityEventKind.ActivelyExploitedVulnerability or SecurityEventKind.VulnerabilityNotExploited;

        // For EF Core only
        private SecurityEvent() { }

        public static SecurityEvent Record(
            Guid projectId, SecurityEventKind kind, string title, string? description, DateTime awareAt,
            Guid userId, UserRole? role, DateTime now)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new DomainException("Choose what happened");
            }

            var cleaned = (title ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException("Give the event a title, e.g. the CVE number");
            }

            if (cleaned.Length > MaxTitleLength)
            {
                throw new DomainException($"The title must be {MaxTitleLength} characters or fewer");
            }

            var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            if (text?.Length > MaxDescriptionLength)
            {
                throw new DomainException($"The description must be {MaxDescriptionLength} characters or fewer");
            }

            if (awareAt > now)
            {
                throw new DomainException("The time you became aware of it cannot be in the future");
            }

            return new SecurityEvent
            {
                ProjectId = projectId,
                Kind = kind,
                Title = cleaned,
                Description = text,
                AwareAt = DateTime.SpecifyKind(awareAt, DateTimeKind.Utc),
                RecordedById = userId,
                RecordedRole = role,
                RecordedAt = now
            };
        }

        // R2: the deadline of a report, or null while it cannot be known yet (the final report waits for a fix or the notification)
        public DateTime? DueAt(SecurityReportKind report) => !IsReportable ? null : report switch
        {
            SecurityReportKind.EarlyWarning => AwareAt + EarlyWarningWithin,
            SecurityReportKind.Notification => AwareAt + NotificationWithin,
            _ when Kind == SecurityEventKind.ActivelyExploitedVulnerability => FixAvailableAt + VulnerabilityFinalReportAfterFix,
            _ => NotificationSentAt?.AddMonths(IncidentFinalReportMonthsAfterNotification)
        };

        public DateTime? SentAt(SecurityReportKind report) => report switch
        {
            SecurityReportKind.EarlyWarning => EarlyWarningSentAt,
            SecurityReportKind.Notification => NotificationSentAt,
            _ => FinalReportSentAt
        };

        public string? Reference(SecurityReportKind report) => report switch
        {
            SecurityReportKind.EarlyWarning => EarlyWarningReference,
            SecurityReportKind.Notification => NotificationReference,
            _ => FinalReportReference
        };

        // R3: where a report stands at a moment
        public SecurityReportStatus Status(SecurityReportKind report, DateTime now)
        {
            // US-127 R1: nothing to report for these kinds
            if (!IsReportable)
            {
                return SecurityReportStatus.NotRequired;
            }

            if (SentAt(report) is { } sent)
            {
                return DueAt(report) is { } due && sent > due ? SecurityReportStatus.SentLate : SecurityReportStatus.Sent;
            }

            if (DueAt(report) is not { } deadline)
            {
                return SecurityReportStatus.Waiting;
            }

            if (now > deadline)
            {
                return SecurityReportStatus.Missed;
            }

            return deadline - now <= TimeSpan.FromHours(24) ? SecurityReportStatus.DueSoon : SecurityReportStatus.Upcoming;
        }

        // R4: the user sent it themselves (e.g. via ENISA's single reporting platform) and gives the platform's reference
        public void MarkSent(SecurityReportKind report, DateTime sentAt, string? reference, DateTime now)
        {
            EnsureOpen();
            if (!IsReportable)
            {
                throw new DomainException("This event has no reports; upgrade it if it becomes reportable");
            }

            if (!Enum.IsDefined(report))
            {
                throw new DomainException("Unknown report");
            }

            if (SentAt(report) is not null)
            {
                throw new DomainException("The report is already marked as sent");
            }

            var text = (reference ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Give the reference you got for the report, e.g. the platform's case number");
            }

            if (text.Length > MaxReferenceLength)
            {
                throw new DomainException($"The reference must be {MaxReferenceLength} characters or fewer");
            }

            if (sentAt > now)
            {
                throw new DomainException("The time it was sent cannot be in the future");
            }

            if (sentAt < AwareAt)
            {
                throw new DomainException("A report cannot be sent before you became aware of the event");
            }

            var at = DateTime.SpecifyKind(sentAt, DateTimeKind.Utc);
            switch (report)
            {
                case SecurityReportKind.EarlyWarning:
                    (EarlyWarningSentAt, EarlyWarningReference) = (at, text);
                    break;
                case SecurityReportKind.Notification:
                    (NotificationSentAt, NotificationReference) = (at, text);
                    break;
                default:
                    (FinalReportSentAt, FinalReportReference) = (at, text);
                    break;
            }
        }

        // Art. 14(2)(c): from now the final report on the vulnerability has a deadline
        public void MarkFixAvailable(DateTime at, DateTime now)
        {
            EnsureOpen();
            if (!IsVulnerability)
            {
                throw new DomainException("Only a vulnerability has a fix");
            }

            if (FixAvailableAt is not null)
            {
                throw new DomainException("The fix is already marked as available");
            }

            if (at > now || at < AwareAt)
            {
                throw new DomainException("The fix must be available between the time you became aware and now");
            }

            FixAvailableAt = DateTime.SpecifyKind(at, DateTimeKind.Utc);
        }

        // R1: closed when every report is sent. US-127 R5: a vulnerability that is not exploited when it is fixed and
        // disclosed, or with a reason why not; an incident that is not severe at any time.
        public void Close(Guid userId, DateTime now, string? reason = null)
        {
            EnsureOpen();
            var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (text?.Length > MaxDescriptionLength)
            {
                throw new DomainException($"The reason must be {MaxDescriptionLength} characters or fewer");
            }

            switch (Kind)
            {
                case SecurityEventKind.VulnerabilityNotExploited when (FixAvailableAt is null || DisclosedAt is null) && text is null:
                    throw new DomainException("Mark the fix as available and the vulnerability as disclosed, or give a reason to close it");
                case SecurityEventKind.ActivelyExploitedVulnerability or SecurityEventKind.SevereIncident
                    when Enum.GetValues<SecurityReportKind>().Any(r => SentAt(r) is null):
                    throw new DomainException("Send every report before closing the event");
            }

            ClosedAt = now;
            ClosedById = userId;
            ClosedReason = text;
        }

        // US-127 (CRA Annex I, Part II (4)): the vulnerability is disclosed after the fix
        public void MarkDisclosed(DateTime at, DateTime now)
        {
            EnsureOpen();
            if (Kind != SecurityEventKind.VulnerabilityNotExploited)
            {
                throw new DomainException("Only a vulnerability that is not exploited is disclosed here");
            }

            if (DisclosedAt is not null)
            {
                throw new DomainException("The vulnerability is already marked as disclosed");
            }

            if (FixAvailableAt is not { } fix || at < fix || at > now)
            {
                throw new DomainException("Disclose the vulnerability after the fix is available, and not in the future");
            }

            DisclosedAt = DateTime.SpecifyKind(at, DateTimeKind.Utc);
        }

        // US-127 R2–R4: it became actively exploited or severe; from reportableAt the reports and deadlines apply
        public void Upgrade(DateTime reportableAt, string? reason, Guid userId, DateTime now)
        {
            EnsureOpen();
            if (IsReportable)
            {
                throw new DomainException(UpgradedAt is null ? "The event is reportable already" : "The event is upgraded already");
            }

            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Say why it became reportable");
            }

            if (text.Length > MaxDescriptionLength)
            {
                throw new DomainException($"The reason must be {MaxDescriptionLength} characters or fewer");
            }

            if (reportableAt < AwareAt || reportableAt > now)
            {
                throw new DomainException("It became reportable between the time you became aware of the event and now");
            }

            FirstAwareAt = AwareAt;
            AwareAt = DateTime.SpecifyKind(reportableAt, DateTimeKind.Utc);
            Kind = Kind == SecurityEventKind.VulnerabilityNotExploited
                ? SecurityEventKind.ActivelyExploitedVulnerability
                : SecurityEventKind.SevereIncident;
            UpgradedAt = now;
            UpgradedById = userId;
            UpgradeReason = text;
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new DomainException("The event is closed");
            }
        }
    }

    // Stored as text
    public enum SecurityEventKind
    {
        // Art. 14(1)
        ActivelyExploitedVulnerability,

        // Art. 14(3)
        SevereIncident,

        // US-127: handled under Annex I, Part II without reports; can be upgraded when it becomes exploited or severe
        VulnerabilityNotExploited,
        IncidentNotSevere
    }

    // Art. 14(2)(a)–(c) and (4)(a)–(c)
    public enum SecurityReportKind
    {
        EarlyWarning,
        Notification,
        FinalReport
    }

    public enum SecurityReportStatus
    {
        // The deadline is not known yet
        Waiting,
        Upcoming,

        // R3: due within 24 hours
        DueSoon,
        Missed,
        Sent,
        SentLate,

        // US-127: the event is not reportable
        NotRequired
    }

    public static class SecurityReportNames
    {
        public static string Title(SecurityReportKind report) => report switch
        {
            SecurityReportKind.EarlyWarning => "Early warning",
            SecurityReportKind.Notification => "Notification",
            _ => "Final report"
        };

        public static string Title(SecurityEventKind kind) => kind switch
        {
            SecurityEventKind.ActivelyExploitedVulnerability => "Actively exploited vulnerability",
            SecurityEventKind.SevereIncident => "Severe incident",
            SecurityEventKind.VulnerabilityNotExploited => "Vulnerability, not exploited",
            _ => "Incident, not severe"
        };
    }
}
