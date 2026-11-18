namespace ComplianceTracker.Domain.Users
{
    // A member's role in the organisation (US-010). What each role may do is in RolePermissions.
    public enum UserRole
    {
        // The organisation admin: everything, incl. members and roles. The creator of an organisation is Admin.
        Admin,

        // Responsible for frameworks, requirements and assessments
        ComplianceManager,

        // Builds the products and links work items to requirements
        Developer,

        // Reads everything, including the log, but changes nothing
        Auditor,

        // US-187: an external advisor who works for several organisations; may do what a Compliance manager may
        Consultant
    }
}
