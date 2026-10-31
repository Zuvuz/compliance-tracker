using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.ActivityLog
{
    // One line in the activity log: who did what, in which organisation, and when.
    // Entries are append-only: there are no setters, and no code changes or deletes them.
    public class ActivityLogEntry
    {
        public Guid OrganizationId { get; }

        // The user who performed the action
        public Guid UserId { get; }

        public ActivityAction Action { get; }

        // Always UTC
        public DateTime OccurredAt { get; }

        // What the entry is about, e.g. the framework version for CatalogImported.
        // Null when the action is not about anything in particular (e.g. signing in).
        public Guid? SubjectId { get; }

        // What changed, in plain text, e.g. "Phase: Planning → Development" (US-024 R3). Null for most actions.
        public string? Details { get; }

        // EF Core also uses this constructor when it reads entries from the database
        public ActivityLogEntry(Guid organizationId, Guid userId, ActivityAction action, DateTime occurredAt, Guid? subjectId = null, string? details = null)
        {
            Details = details;
            OrganizationId = organizationId;
            UserId = userId;
            Action = action;
            OccurredAt = occurredAt;
            SubjectId = subjectId;
        }
    }
}
