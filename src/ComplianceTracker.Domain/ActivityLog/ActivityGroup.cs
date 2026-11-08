namespace ComplianceTracker.Domain.ActivityLog
{
    // US-101: the actions in groups a person looks for, so a filter shows ten groups instead of every action
    public enum ActivityGroup
    {
        SignIn,
        Members,
        Organisation,
        Frameworks,
        Projects,
        WorkItems,
        Requirements,
        Evidence,
        Risks,
        Ai,

        // US-073
        SecurityEvents
    }

    public static class ActivityGroups
    {
        // Every action belongs to exactly one group; a new action without a group stops the build (no default)
        public static ActivityGroup Of(ActivityAction action) => action switch
        {
            ActivityAction.UserSignedIn or ActivityAction.UserSignedOut => ActivityGroup.SignIn,
            ActivityAction.MemberRoleChanged or ActivityAction.InvitationCreated or ActivityAction.InvitationRevoked
                or ActivityAction.InvitationAccepted or ActivityAction.MemberRemoved => ActivityGroup.Members,
            ActivityAction.OrganizationCreated or ActivityAction.FeedbackSent or ActivityAction.FeedbackIssueCreated
                or ActivityAction.FeedbackDismissed => ActivityGroup.Organisation,
            ActivityAction.CatalogImported or ActivityAction.CatalogActivated or ActivityAction.FrameworkEnabled
                or ActivityAction.FrameworkDisabled => ActivityGroup.Frameworks,
            ActivityAction.ProjectCreated or ActivityAction.ProjectUpdated or ActivityAction.ProjectPhaseChanged
                or ActivityAction.SupportPeriodSet
                or ActivityAction.ProjectClassified
                or ActivityAction.ProjectFrameworkAdded or ActivityAction.ProjectFrameworkRemoved
                or ActivityAction.ProjectFrameworkVersionChanged or ActivityAction.ProjectMethodsChanged
                or ActivityAction.ApprovalRequested or ActivityAction.ApprovalGiven or ActivityAction.ApprovalRejected
                or ActivityAction.ApprovalWithdrawn or ActivityAction.ApprovalCancelled or ActivityAction.ApprovalExpired
                or ActivityAction.ReportLocked => ActivityGroup.Projects,
            ActivityAction.RepositoryConnected or ActivityAction.WorkItemsSynced or ActivityAction.AccessTokenChanged
                or ActivityAction.RequirementDocumentUploaded => ActivityGroup.WorkItems,
            ActivityAction.RequirementLinked or ActivityAction.RequirementUnlinked or ActivityAction.RequirementMarkedNotApplicable
                or ActivityAction.RequirementMarkedApplicable or ActivityAction.LinkReviewed or ActivityAction.ReportDownloaded or ActivityAction.ApplicabilitySuggested
                or ActivityAction.ApplicabilitySuggestionAccepted or ActivityAction.ApplicabilitySuggestionRejected => ActivityGroup.Requirements,
            ActivityAction.EvidenceAdded or ActivityAction.EvidenceRemoved or ActivityAction.EvidenceLinked
                or ActivityAction.EvidenceUnlinked or ActivityAction.EvidenceLinkSuggested or ActivityAction.EvidenceLinkApproved
                or ActivityAction.EvidenceLinkRejected or ActivityAction.EvidenceSuggested or ActivityAction.EvidenceSuggestionAccepted
                or ActivityAction.EvidenceSuggestionRejected
                or ActivityAction.OrganisationDocumentUploaded or ActivityAction.OrganisationDocumentReviewed
                or ActivityAction.OrganisationDocumentLinked or ActivityAction.OrganisationDocumentUnlinked
                or ActivityAction.OrganisationDocumentRemoved => ActivityGroup.Evidence,
            ActivityAction.RiskAdded or ActivityAction.RiskUpdated or ActivityAction.RiskReviewed
                or ActivityAction.RiskActionAdded or ActivityAction.RiskActionLinked or ActivityAction.RiskActionDone => ActivityGroup.Risks,
            ActivityAction.VulnerabilityRecorded or ActivityAction.IncidentRecorded or ActivityAction.SecurityReportSent
                or ActivityAction.SecurityFixAvailable or ActivityAction.SecurityEventClosed
                or ActivityAction.SecurityEventUpgraded or ActivityAction.SecurityEventDisclosed
                or ActivityAction.SecurityEventSuggested or ActivityAction.SecurityEventSuggestionAccepted
                or ActivityAction.SecurityEventSuggestionDismissed
                or ActivityAction.ScanImported or ActivityAction.FindingAccepted
                or ActivityAction.ScanUploadTokenCreated or ActivityAction.ScanUploadTokenRevoked => ActivityGroup.SecurityEvents,
            ActivityAction.MappingsSuggested or ActivityAction.MappingApproved or ActivityAction.MappingRejected
                or ActivityAction.AiFeedbackSharingChanged or ActivityAction.ProjectAiSharingChanged or ActivityAction.AiFeedbackExported
                or ActivityAction.TrainingDataExported => ActivityGroup.Ai,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "The action has no group; add it in ActivityGroups.Of")
        };

        public static IReadOnlyList<ActivityAction> ActionsIn(ActivityGroup group)
            => Enum.GetValues<ActivityAction>().Where(a => Of(a) == group).ToList();

        public static string Title(ActivityGroup group) => group switch
        {
            ActivityGroup.SignIn => "Sign-in",
            ActivityGroup.Members => "Members",
            ActivityGroup.Organisation => "Organisation",
            ActivityGroup.Frameworks => "Frameworks",
            ActivityGroup.Projects => "Projects",
            ActivityGroup.WorkItems => "User stories and documents",
            ActivityGroup.Requirements => "Requirements and links",
            ActivityGroup.Evidence => "Evidence",
            ActivityGroup.Risks => "Risks",
            ActivityGroup.SecurityEvents => "Security events",
            _ => "AI"
        };
    }
}
