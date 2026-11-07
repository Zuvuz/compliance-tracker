using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Security
{
    // US-128: a security event the tracker suggests from what it knows: an open finding whose CVE is on a public list of
    // exploited vulnerabilities, or a new critical finding. R1: it is never an event until a person records it.
    public class SecurityEventSuggestion
    {
        public const int MaxReasonLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }

        // The CVE id, e.g. "CVE-2026-1234", or "finding:<id>" for a critical finding without a CVE
        public string Key { get; private set; } = "";
        public string Title { get; private set; } = "";
        public SuggestionSource Source { get; private set; }

        // The list ("CISA KEV", "EUVD") or the scanner that found it
        public string SourceName { get; private set; } = "";
        public SecurityEventKind Kind { get; private set; }

        // R4: an event of US-127 for the same CVE that should be upgraded instead
        public Guid? UpgradeEventId { get; private set; }

        // R6: the findings it is based on; a dismissed suggestion comes back only for a finding not in this list
        public List<Guid> FindingIds { get; private set; } = [];

        // R1: when the tracker learned it; the awareness time when it is recorded
        public DateTime LearnedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public string? DismissedReason { get; private set; }
        public Guid? DecidedById { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public Guid? EventId { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;
        public bool IsUpgrade => UpgradeEventId is not null;

        // For EF Core only
        private SecurityEventSuggestion() { }

        public static SecurityEventSuggestion Create(
            Guid projectId, string key, string title, SuggestionSource source, string sourceName, SecurityEventKind kind,
            Guid? upgradeEventId, IEnumerable<Guid> findingIds, DateTime learnedAt)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new DomainException("A suggestion needs what it is about");
            }

            if (kind is not (SecurityEventKind.ActivelyExploitedVulnerability or SecurityEventKind.VulnerabilityNotExploited))
            {
                throw new DomainException("Only vulnerabilities are suggested");
            }

            if (upgradeEventId is not null && kind != SecurityEventKind.ActivelyExploitedVulnerability)
            {
                throw new DomainException("Only an actively exploited vulnerability is suggested as an upgrade");
            }

            var cleaned = (title ?? "").Trim();
            if (cleaned.Length > SecurityEvent.MaxTitleLength)
            {
                cleaned = cleaned[..(SecurityEvent.MaxTitleLength - 1)].TrimEnd() + "…";
            }

            return new SecurityEventSuggestion
            {
                ProjectId = projectId,
                Key = key.Trim(),
                Title = cleaned.Length == 0 ? key.Trim() : cleaned,
                Source = source,
                SourceName = sourceName,
                Kind = kind,
                UpgradeEventId = upgradeEventId,
                FindingIds = findingIds.Distinct().ToList(),
                LearnedAt = DateTime.SpecifyKind(learnedAt, DateTimeKind.Utc)
            };
        }

        // R1: a person recorded it (or upgraded the event); the event is linked when it exists
        public void Accept(Guid userId, DateTime now)
        {
            EnsurePending();
            Status = SuggestionStatus.Accepted;
            DecidedById = userId;
            DecidedAt = now;
        }

        public void LinkEvent(Guid eventId) => EventId = eventId;

        // R6: dismissed with a reason
        public void Dismiss(string? reason, Guid userId, DateTime now)
        {
            EnsurePending();
            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Say why the suggestion is dismissed");
            }

            if (text.Length > MaxReasonLength)
            {
                throw new DomainException($"The reason must be {MaxReasonLength} characters or fewer");
            }

            Status = SuggestionStatus.Dismissed;
            DismissedReason = text;
            DecidedById = userId;
            DecidedAt = now;
        }

        // The tracker withdraws it, e.g. a "not exploited" suggestion when the CVE is now actively exploited
        public void Supersede(string reason, DateTime now)
        {
            EnsurePending();
            Status = SuggestionStatus.Dismissed;
            DismissedReason = reason;
            DecidedAt = now;
        }

        private void EnsurePending()
        {
            if (!IsPending)
            {
                throw new DomainException("The suggestion is already decided");
            }
        }
    }

    public enum SuggestionSource
    {
        ExploitedList,
        CriticalFinding
    }

    public enum SuggestionStatus
    {
        Pending,
        Accepted,
        Dismissed
    }

    // US-128 R5: a public list of exploited vulnerabilities, as the tracker last fetched it
    public class ExploitedList
    {
        public const int MaxErrorLength = 500;

        // e.g. "CISA KEV"
        public string Name { get; private set; } = "";
        public List<string> Cves { get; private set; } = [];
        public DateTime? FetchedAt { get; private set; }
        public DateTime? AttemptedAt { get; private set; }
        public string? LastError { get; private set; }

        // For EF Core only
        private ExploitedList() { }

        public ExploitedList(string name)
        {
            Name = name;
        }

        public void Refreshed(IEnumerable<string> cves, DateTime at)
        {
            Cves = cves.Select(c => c.Trim().ToUpperInvariant()).Where(c => c.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList();
            FetchedAt = at;
            AttemptedAt = at;
            LastError = null;
        }

        // R5: the last fetched list is kept
        public void Failed(string error, DateTime at)
        {
            AttemptedAt = at;
            LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        }
    }
}
