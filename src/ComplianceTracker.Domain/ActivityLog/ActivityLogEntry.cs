using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.ActivityLog
{
    public class ActivityLogEntry
    {
        public Guid OrganizationId { get; }
        public Guid UserId { get; }
        public ActivityAction Action { get; }
        public DateTime OccurredAt { get; }

        public ActivityLogEntry(Guid organizationId, Guid userId, ActivityAction action, DateTime occurredAt)
        {
            OrganizationId = organizationId;
            UserId = userId;
            Action = action;
            OccurredAt = occurredAt;
        }
    }
}
