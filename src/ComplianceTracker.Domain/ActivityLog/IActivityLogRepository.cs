using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.ActivityLog
{
    public interface IActivityLogRepository
    {
        void Append(ActivityLogEntry entry);
    }
}
