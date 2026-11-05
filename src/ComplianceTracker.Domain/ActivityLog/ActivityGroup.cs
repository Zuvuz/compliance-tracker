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
                or ActivityAction.ProjectFrameworkAdded or ActivityAction.ProjectFrameworkRemoved
                or ActivityAction.ProjectFrameworkVersionChanged or ActivityAction.ProjectMethodsChanged => ActivityGroup.Projects,
            ActivityAction.RepositoryConnected or ActivityAction.WorkItemsSynced or ActivityAction.AccessTokenChanged
                or ActivityAction.RequirementDocumentUploaded => ActivityGroup.WorkItems,
            ActivityAction.RequirementLinked or ActivityAction.RequirementUnlinked or ActivityAction.RequirementMarkedNotApplicable
                or ActivityAction.RequirementMarkedApplicable => ActivityGroup.Requirements,
            ActivityAction.EvidenceAdded or ActivityAction.EvidenceRemoved or ActivityAction.EvidenceLinked
                or ActivityAction.EvidenceUnlinked or ActivityAction.EvidenceLinkSuggested or ActivityAction.EvidenceLinkApproved
                or ActivityAction.EvidenceLinkRejected or ActivityAction.EvidenceSuggested or ActivityAction.EvidenceSuggestionAccepted
                or ActivityAction.EvidenceSuggestionRejected => ActivityGroup.Evidence,
            ActivityAction.RiskAdded or ActivityAction.RiskUpdated or ActivityAction.RiskReviewed => ActivityGroup.Risks,
            ActivityAction.VulnerabilityRecorded or ActivityAction.IncidentRecorded or ActivityAction.SecurityReportSent
                or ActivityAction.SecurityFixAvailable or ActivityAction.SecurityEventClosed => ActivityGroup.SecurityEvents,
            ActivityAction.MappingsSuggested or ActivityAction.MappingApproved or ActivityAction.MappingRejected
                or ActivityAction.AiFeedbackSharingChanged or ActivityAction.AiFeedbackExported => ActivityGroup.Ai,
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
