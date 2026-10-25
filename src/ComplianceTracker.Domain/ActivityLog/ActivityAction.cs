using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.ActivityLog
{
    public enum ActivityAction
    {
        OrganizationCreated,
        UserSignedIn,
        UserSignedOut,
        CatalogImported
    }
}
