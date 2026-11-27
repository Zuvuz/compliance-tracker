using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.ActivityLog
{
    // US-219: what a visit opened. A project tab, or one sensitive item (R1). Lists without details are the tab.
    public enum VisitKind
    {
        Tab,
        Risk,
        SecurityEvent,
        Evidence,
        Report,
        AuditPackage,
        Supplier,
        Approval
    }

    // US-219: one member's visit to a page with sensitive data: who, in which role, what was opened, when, and the time spent
    // while the page was visible (R1, R2). A visit is never changed by a person, only deleted at the end of the
    // retention period (R9); its signs of life only move "last seen" and the time spent forward.
    public class PageVisit
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid UserId { get; private set; }
        public UserRole Role { get; private set; } // the role at the time
        public Guid? ProjectId { get; private set; }
        public VisitKind Kind { get; private set; }
        public Guid? ItemId { get; private set; }
        public string? Tab { get; private set; }

        // What was opened, as it was named then, e.g. "Risk: Data leak"; the item may be renamed or deleted later
        public string Title { get; private set; } = "";

        public DateTime StartedAt { get; private set; }
        public DateTime LastSeenAt { get; private set; }

        // R2: only time while the page was visible counts
        public int SecondsSpent { get; private set; }

        // Whether the page was visible at the last sign of life; the time until the next sign counts only then
        public bool Visible { get; private set; }

        // The page said goodbye (it was left or closed); later signs are ignored
        public bool Ended { get; private set; }

        private PageVisit()
        {
        }

        public static PageVisit Start(
            Guid organizationId, Guid userId, UserRole role, Guid? projectId, VisitKind kind, Guid? itemId, string? tab, string title, DateTime at)
        {
            if (kind != VisitKind.Tab && itemId is null)
            {
                throw new DomainException("A visit to one item needs the item");
            }

            return new PageVisit
            {
                OrganizationId = organizationId,
                UserId = userId,
                Role = role,
                ProjectId = projectId,
                Kind = kind,
                ItemId = itemId,
                Tab = string.IsNullOrWhiteSpace(tab) ? null : tab.Trim(),
                Title = string.IsNullOrWhiteSpace(title) ? kind.ToString() : title.Trim(),
                StartedAt = at,
                LastSeenAt = at,
                Visible = true
            };
        }

        // R2: a visit ends 2 minutes after its last sign of life, or when the page says goodbye
        public bool IsOver(DateTime now) => Ended || now - LastSeenAt > AccessLogRules.EndsAfter;

        // R3: on the page now, with a sign of life within the last 2 minutes
        public bool IsOnPage(DateTime now) => !IsOver(now);

        public TimeSpan TimeSpent => TimeSpan.FromSeconds(SecondsSpent);

        // R2: a sign of life every 30 seconds, saying whether the page is visible, and one when it is hidden or shown again.
        // The time since the last sign counts only when the page was visible then. A sign after the visit is over (a
        // browser that slept, a member who came back after 2 minutes) changes nothing: the page starts a new visit.
        // Returns false when the sign was ignored.
        public bool SignOfLife(DateTime now, bool visible)
        {
            if (IsOver(now) || now < LastSeenAt)
            {
                return false;
            }

            Count(now);
            Visible = visible;
            return true;
        }

        // The page was left; the time since the last sign counts as for a sign of life
        public bool End(DateTime now)
        {
            if (IsOver(now) || now < LastSeenAt)
            {
                return false;
            }

            Count(now);
            Visible = false;
            Ended = true;
            return true;
        }

        private void Count(DateTime now)
        {
            if (Visible)
            {
                SecondsSpent += (int)Math.Round((now - LastSeenAt).TotalSeconds, MidpointRounding.AwayFromZero);
            }

            LastSeenAt = now;
        }
    }

    // US-219: the access log's fixed rules
    public static class AccessLogRules
    {
        public static readonly TimeSpan SignOfLifeEvery = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan EndsAfter = TimeSpan.FromMinutes(2);

        // R7: the members are told at least 6 weeks before it starts (Danish practice for control measures)
        public const int NoticeDays = 42;

        // R6: kept 12 months by default, at most 5 years
        public const int DefaultRetentionMonths = 12;
        public const int MinRetentionMonths = 1;
        public const int MaxRetentionMonths = 60;

        // R4: the roles that see the access log; everyone who can read keeps the change log
        public static bool CanSee(UserRole role) => role is UserRole.Admin or UserRole.ComplianceManager or UserRole.Auditor;

        // R6: a visit is deleted when it started before this
        public static DateTime DeleteBefore(DateTime now, int retentionMonths) => now.AddMonths(-retentionMonths);

        // The time one member spent on one item: two tabs open at the same time count once. The visits are joined where
        // they overlap, and the total is never more than the time they cover together.
        public static TimeSpan TotalTime(IEnumerable<PageVisit> visits)
        {
            var list = visits.OrderBy(v => v.StartedAt).ToList();
            var spent = TimeSpan.FromSeconds(list.Sum(v => v.SecondsSpent));
            var covered = TimeSpan.Zero;
            DateTime? from = null, to = null;
            foreach (var visit in list)
            {
                if (from is null || visit.StartedAt > to)
                {
                    if (from is not null)
                    {
                        covered += to!.Value - from.Value;
                    }

                    (from, to) = (visit.StartedAt, visit.LastSeenAt);
                }
                else if (visit.LastSeenAt > to)
                {
                    to = visit.LastSeenAt;
                }
            }

            if (from is not null)
            {
                covered += to!.Value - from.Value;
            }

            return spent < covered ? spent : covered;
        }

        // e.g. "8 minutes", "1 minute", "less than a minute", "1 h 5 min"
        public static string Text(TimeSpan spent)
        {
            var minutes = (int)Math.Round(spent.TotalMinutes, MidpointRounding.AwayFromZero);
            return minutes switch
            {
                < 1 => "less than a minute",
                1 => "1 minute",
                < 60 => $"{minutes} minutes",
                _ => $"{minutes / 60} h {minutes % 60} min"
            };
        }
    }
}
