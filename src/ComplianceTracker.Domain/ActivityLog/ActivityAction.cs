using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.ActivityLog
{
    // The kinds of actions that are written to the activity log (ISO 27001 A.8.15).
    // Stored as text in the database, so new values never shift existing data.
    public enum ActivityAction
    {
        // An organisation was created; the creator became its Admin (US-001)
        OrganizationCreated,

        // A member signed in or out (US-004). The very first sign-in is not logged,
        // because the user has no organisation yet; OrganizationCreated covers it.
        UserSignedIn,
        UserSignedOut,

        // A framework PDF was uploaded as a new version (US-005).
        // The log entry's SubjectId points at the framework version.
        CatalogImported,

        // A project was created (US-008). The log entry's SubjectId points at the project.
        ProjectCreated,

        // A built-in framework was switched on or off for the organisation (US-006).
        // The log entry's SubjectId points at the framework version.
        FrameworkEnabled,
        FrameworkDisabled,

        // A project started using a framework, stopped using it, or moved to another version (US-011).
        // The log entry's SubjectId points at the project; the ProjectFramework rows hold the versions.
        ProjectFrameworkAdded,
        ProjectFrameworkRemoved,
        ProjectFrameworkVersionChanged
    }
}
