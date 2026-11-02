namespace ComplianceTracker.Domain.Users
{
    // What a member may do (US-010 R4). Every use case that changes data asks for one of these (R5).
    public enum Permission
    {
        // Members, roles and invitations
        ManageMembers,

        // Whether the organisation's AI decisions may be exported (US-036)
        ManageAiSharing,

        // Upload frameworks, correct and activate versions, switch built-in frameworks on and off
        ManageFrameworks,

        // Decisions that change the compliance status: approve/reject AI suggestions, "not applicable",
        // accept and close risks
        DecideCompliance,

        // Create and edit projects, their frameworks and their GitHub link
        EditProjects,

        // Link user stories to requirements by hand, sync, and ask the AI for suggestions
        LinkWork,

        // Add, edit and review risks and their measures
        ManageRisks
    }

    // US-010 R4: the permission table. The organisation's Admin may do everything; the Auditor only reads.
    public static class RolePermissions
    {
        private static readonly IReadOnlyDictionary<UserRole, Permission[]> Table = new Dictionary<UserRole, Permission[]>
        {
            [UserRole.Admin] = Enum.GetValues<Permission>(),
            [UserRole.ComplianceManager] =
            [
                Permission.ManageFrameworks, Permission.DecideCompliance, Permission.EditProjects,
                Permission.LinkWork, Permission.ManageRisks
            ],
            [UserRole.Developer] = [Permission.EditProjects, Permission.LinkWork, Permission.ManageRisks],
            [UserRole.Auditor] = []
        };

        public static bool Can(UserRole role, Permission permission)
            => Table.TryGetValue(role, out var permissions) && permissions.Contains(permission);

        public static IReadOnlyList<Permission> Of(UserRole role) => Table.TryGetValue(role, out var p) ? p : [];

        // How a role is shown, e.g. "Compliance manager"
        public static string Title(UserRole role) => role switch
        {
            UserRole.Admin => "Organisation admin",
            UserRole.ComplianceManager => "Compliance manager",
            _ => role.ToString()
        };
    }
}
