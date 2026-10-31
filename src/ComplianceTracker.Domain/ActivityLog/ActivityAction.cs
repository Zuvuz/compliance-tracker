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

        // A framework version was activated, which locks its requirements (US-007).
        // The log entry's SubjectId points at the framework version.
        CatalogActivated,

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
        ProjectFrameworkVersionChanged,

        // A project's details were edited, or it moved to another lifecycle phase (US-024).
        // The log entry's SubjectId points at the project; Details holds the old and new values.
        ProjectUpdated,
        ProjectPhaseChanged,

        // A project was linked to a GitHub repository, or its user stories were read from it (US-018).
        // The log entry's SubjectId points at the project; Details holds the repository or the sync's result.
        // A scheduled sync has no user (UserId is null: the system did it).
        RepositoryConnected,
        WorkItemsSynced,

        // A project's GitHub token was set, replaced or removed (US-031). Details never holds the token.
        AccessTokenChanged
    }
}
