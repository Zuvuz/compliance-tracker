using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Web.Filters;
using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Models
{
    // The fields of the create-project form (US-008). No validation rules here: the domain enforces them.
    // The text fields are nullable, so MVC does not create its own "required" errors but lets the domain answer.
    // IFormModel: on a broken rule, DomainExceptionFilter shows the form again with what the user typed
    public class CreateProjectViewModel : IFormModel
    {
        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Intended purpose")]
        public string? IntendedPurpose { get; set; }

        [Display(Name = "Scope")]
        public string? Scope { get; set; }

        // Defaults to Planning when the user does not choose another phase
        [Display(Name = "Lifecycle phase")]
        public LifecyclePhase LifecyclePhase { get; set; } = LifecyclePhase.Planning;

        // US-011 R2: the ticked frameworks (the newest version of each is used).
        // The checkboxes themselves come from the FrameworkChoices view component.
        public List<Guid> FrameworkIds { get; set; } = new();
    }

    // The fields of the edit-project form (US-024 R1). Like the create form, the domain checks the values.
    public class EditProjectViewModel : IFormModel
    {
        public Guid Id { get; set; }

        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Intended purpose")]
        public string? IntendedPurpose { get; set; }

        [Display(Name = "Scope")]
        public string? Scope { get; set; }

        public static EditProjectViewModel From(Project project) => new()
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            IntendedPurpose = project.IntendedPurpose,
            Scope = project.Scope
        };
    }

    // The framework checkboxes on the create-project form (Views/Shared/Components/FrameworkChoices)
    // US-007: only frameworks with an active version are offered
    public record FrameworkChoicesViewModel(IReadOnlyList<Framework> Frameworks, IReadOnlyCollection<Guid> Selected);

    // The tabs of the project page and their addresses (US-022 R2, R6): /Projects/Details/{id}/{slug}
    public static class ProjectTabs
    {
        public const string Overview = "overview";
        public const string Frameworks = "frameworks";
        public const string UserStories = "user-stories";
        public const string Activity = "activity";

        // In the order they are shown, with the text on the tab
        public static readonly IReadOnlyList<(string Slug, string Title)> All =
        [
            (Overview, "Overview"),
            (Frameworks, "Frameworks"),
            (UserStories, "User stories"),
            (Activity, "Activity")
        ];

        // The tab for an address, Overview when there is none, or null for an unknown one
        public static string? Find(string? slug)
            => string.IsNullOrEmpty(slug) ? Overview : All.Select(t => t.Slug).FirstOrDefault(t => t == slug.ToLowerInvariant());
    }

    // One framework a project follows, with the version it uses (US-011 R6) and who added it (US-022 R4)
    public record ProjectFrameworkRow(Framework Framework, FrameworkVersion Version, ProjectFramework Link)
    {
        // US-007: only an activated version counts as newer; drafts cannot be used yet
        public bool NewerVersionExists => Framework.CurrentVersion is { } current && current.Id != Version.Id
            && current.Number > Version.Number;

        public string Status => NewerVersionExists ? "Newer version available" : "Newest";
    }

    // The project's user stories from GitHub and how the last sync went (Views/Projects/_WorkItems.cshtml, US-018)
    public record WorkItemsViewModel(Project Project, IReadOnlyList<WorkItem> WorkItems);

    // Data for the project page (Views/Projects/Details.cshtml and its tabs)
    public record ProjectDetailsViewModel(Project Project, IReadOnlyList<Framework> AvailableFrameworks, IReadOnlyList<WorkItem> WorkItems)
    {
        // US-022: the tab being shown (ProjectTabs), the organisation (for names) and the Activity tab's log entries
        public string Tab { get; init; } = ProjectTabs.Overview;
        public Organization? Organization { get; init; }
        public IReadOnlyList<ActivityLogEntry> Activity { get; init; } = [];

        public WorkItemsViewModel WorkItemsSection => new(Project, WorkItems);

        // The name of a member, or "System" for what the tracker did by itself (US-018 R6)
        public string NameOf(Guid? userId)
            => userId is null ? "System" : Organization?.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";

        // US-022 R3, e.g. "2 frameworks, 1 not on newest version"
        public string FrameworkSummary
        {
            get
            {
                if (Frameworks.Count == 0)
                {
                    return "No frameworks yet";
                }

                var outdated = Frameworks.Count(f => f.NewerVersionExists);
                return $"{Frameworks.Count} framework{(Frameworks.Count == 1 ? "" : "s")}, {outdated} not on newest version";
            }
        }

        public string WorkItemSummary
            => WorkItems.Count == 0
                ? "No user stories yet"
                : $"{WorkItems.Count} user stor{(WorkItems.Count == 1 ? "y" : "ies")}, {WorkItems.Count(w => w.State == WorkItemState.Open)} open";

        // The project's frameworks, in alphabetical order
        public IReadOnlyList<ProjectFrameworkRow> Frameworks { get; } = Project.Frameworks
            .Select(link =>
            {
                var framework = AvailableFrameworks.FirstOrDefault(f => f.Id == link.FrameworkId);
                var version = framework?.Versions.FirstOrDefault(v => v.Id == link.FrameworkVersionId);
                return framework is null || version is null ? null : new ProjectFrameworkRow(framework, version, link);
            })
            .OfType<ProjectFrameworkRow>()
            .OrderBy(r => r.Framework.Name)
            .ToList();

        // The frameworks that can still be added (the project uses each framework at most once, R3),
        // and that have an active version (US-007)
        public IEnumerable<Framework> CanBeAdded
            => AvailableFrameworks.Where(f => Project.FrameworkInUse(f.Id) is null && f.CurrentVersion is not null);
    }

    // One line in the organisation overview: a framework version and the projects using it (US-011 R5)
    public record FrameworkUsageRow(Framework Framework, FrameworkVersion Version, IReadOnlyList<Project> Projects)
    {
        public bool IsNewest => Version.Id == Framework.CurrentVersion?.Id;
    }

    // Data for the organisation's Overview tab: the organisation, its projects and which frameworks they use
    public record OrganizationOverviewViewModel(
        Organization Organization,
        IReadOnlyList<Project> Projects,
        IReadOnlyList<Framework> Frameworks)
    {
        // Per framework: the newest version (even with no projects yet), then every older version in use
        public IReadOnlyList<FrameworkUsageRow> FrameworkUsage { get; } = Frameworks
            .SelectMany(framework => framework.Versions
                .OrderByDescending(v => v.Number)
                .Select(version => new FrameworkUsageRow(
                    framework,
                    version,
                    Projects.Where(p => p.Frameworks.Any(f => f.FrameworkVersionId == version.Id)).ToList()))
                .Where(row => row.IsNewest || row.Projects.Count > 0))
            .ToList();
    }
}
