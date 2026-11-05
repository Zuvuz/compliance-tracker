namespace ComplianceTracker.Domain.Projects
{
    // US-104: how a project describes its product's requirements. A project uses one or more (R2).
    // Stored as text, e.g. "GitHubUserStories, UseCases".
    [Flags]
    public enum RequirementMethods
    {
        None = 0,

        // US-018: user stories read from a GitHub repository
        GitHubUserStories = 1,

        // US-032, US-033: a requirement specification (Excel, CSV or Word)
        RequirementSpecification = 2,

        // US-034: Unified Process use cases (Word)
        UseCases = 4,

        All = GitHubUserStories | RequirementSpecification | UseCases
    }

    public static class RequirementMethodNames
    {
        // In the order they are shown, with the text on the page
        public static readonly IReadOnlyList<(RequirementMethods Method, string Title)> All =
        [
            (RequirementMethods.GitHubUserStories, "GitHub user stories"),
            (RequirementMethods.RequirementSpecification, "Requirement specification"),
            (RequirementMethods.UseCases, "Use cases")
        ];

        // e.g. "GitHub user stories, Use cases"
        public static string Describe(RequirementMethods methods)
            => string.Join(", ", All.Where(m => methods.HasFlag(m.Method)).Select(m => m.Title));
    }
}
