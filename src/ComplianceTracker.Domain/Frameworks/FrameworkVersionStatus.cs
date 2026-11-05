namespace ComplianceTracker.Domain.Frameworks
{
    // Whether a framework version can still be changed (US-007).
    // Stored as text in the database, so new values never shift existing data.
    public enum FrameworkVersionStatus
    {
        // Just uploaded: the requirements can be checked and corrected. Projects cannot use it yet.
        Draft,

        // Locked: the requirements can no longer change, and projects can use it
        Active
    }
}
