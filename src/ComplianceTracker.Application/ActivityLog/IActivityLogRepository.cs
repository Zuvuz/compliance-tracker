using ComplianceTracker.Domain.ActivityLog;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Application.ActivityLog
{
    public interface IActivityLogRepository
    {
        void Append(ActivityLogEntry entry);
    }
}
