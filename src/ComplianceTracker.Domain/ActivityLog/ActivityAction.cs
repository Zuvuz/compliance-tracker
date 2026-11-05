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
        AccessTokenChanged,

        // A user story was linked to a requirement, or the link was removed (US-021).
        // SubjectId points at the project; Details holds e.g. "SP-1 ↔ [US-004] Sign in".
        RequirementLinked,
        RequirementUnlinked,

        // A requirement was marked as not applicable to a project, or applicable again (US-043).
        // SubjectId points at the project; Details holds the code and the reason.
        RequirementMarkedNotApplicable,
        RequirementMarkedApplicable,

        // The local AI made suggestions for a project, or a person decided one (US-020).
        // SubjectId points at the project; Details holds e.g. "SP-1 ↔ [US-004] Sign in" or the number made.
        MappingsSuggested,
        MappingApproved,
        MappingRejected,

        // An Admin switched sharing of AI feedback on or off, or exported it (US-036). SubjectId is the organisation.
        AiFeedbackSharingChanged,
        AiFeedbackExported,

        // A risk was added to a project's register, changed (incl. accepted, closed, measures) or reviewed (US-042).
        // SubjectId points at the project; Details holds the risk's title and what changed.
        RiskAdded,
        RiskUpdated,
        RiskReviewed,

        // Members and roles (US-009, US-010). SubjectId points at the member or the invitation.
        MemberRoleChanged,
        InvitationCreated,
        InvitationRevoked,
        InvitationAccepted,

        // US-090: a member was removed by an Admin. SubjectId points at the member; Details holds the name.
        MemberRemoved,

        // Evidence was added to or removed from a requirement of a project (US-057).
        // SubjectId points at the project; Details holds e.g. "I.2(d): PR 42".
        EvidenceAdded,
        EvidenceRemoved,

        // A requirement specification was uploaded to a project as a new version (US-032).
        // SubjectId points at the project; Details holds the file name, the version and what it changed.
        RequirementDocumentUploaded,

        // US-063: a member sent feedback, and the system admin made a GitHub issue of it or dismissed it.
        // SubjectId points at the feedback; Details holds its title, the issue's number or the reason.
        FeedbackSent,
        FeedbackIssueCreated,
        FeedbackDismissed,

        // US-059: evidence was linked to or unlinked from a requirement, the AI suggested links, and a person approved or
        // rejected one. SubjectId points at the project; Details holds e.g. "I.2(d): Access policy 2026".
        EvidenceLinked,
        EvidenceUnlinked,
        EvidenceLinkSuggested,
        EvidenceLinkApproved,
        EvidenceLinkRejected,

        // US-058: the tracker suggested evidence from test results or pull requests, and a person accepted or rejected
        // a suggestion. SubjectId points at the project; Details holds the source and the requirement.
        EvidenceSuggested,
        EvidenceSuggestionAccepted,
        EvidenceSuggestionRejected,

        // US-026: a project was classified under a regulation. SubjectId points at the project; Details holds e.g.
        // "CRA: Important, class I (Annex III, class I, point 2: a password manager)".
        ProjectClassified
    }
}
