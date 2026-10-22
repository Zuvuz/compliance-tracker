using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.ActivityLog
{
    public class ActivityLogEntry
    {
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
        public ActivityAction Action { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
