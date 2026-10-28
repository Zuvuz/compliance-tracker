using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Organizations
{
    // Company size as used by the EU (e.g. in NIS2) to decide which rules apply
    public enum OrganizationSize
    {
        Micro,
        Small,
        Medium,
        Large
    }
}
