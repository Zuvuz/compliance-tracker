using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Users
{
    // What a member may do. Only Admin is used so far; the permissions of each role come in US-010.
    public enum UserRole
    {
        // Manages the organisation, its members and their roles. The creator of an organisation is Admin.
        Admin,

        // Responsible for frameworks, requirements and assessments
        ComplianceManager,

        // Builds the products and links work items to requirements
        Developer,

        // Reads everything, including the log, but changes nothing
        Auditor
    }
}
