using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Application.Compliance;
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

        // US-092: the version the form was opened with
        public uint? Version { get; set; }

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
            Version = project.Version,
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
        public const string Compliance = "compliance";
        public const string Evidence = "evidence";
        public const string Risks = "risks";
        public const string SecurityEvents = "security-events";
        public const string Scans = "scans"; // US-078
        // US-104 R1: was "User stories" at "user-stories"; the old address redirects (OldProductRequirements)
        public const string ProductRequirements = "product-requirements";
        public const string OldProductRequirements = "user-stories";
        public const string Activity = "activity";
        public const string Reports = "reports"; // US-129

        // In the order they are shown, with the text on the tab
        public static readonly IReadOnlyList<(string Slug, string Title)> All =
        [
            (Overview, "Overview"),
            (Frameworks, "Frameworks"),
            (Compliance, "Compliance"),
            (Evidence, "Evidence"),
            (Risks, "Risks"),
            (SecurityEvents, "Security events"),
            (Scans, "Scans"),
            (ProductRequirements, "Product requirements"),
            (Activity, "Activity"),
            (Reports, "Reports")
        ];

        // The tab for an address, Overview when there is none, or null for an unknown one
        public static string? Find(string? slug)
            => string.IsNullOrEmpty(slug) ? Overview : All.Select(t => t.Slug).FirstOrDefault(t => t == slug.ToLowerInvariant());
    }

    // US-044 R2: the Compliance tab's filters, as ?show=... so a filtered view can be shared
    public static class ComplianceFilters
    {
        public const string All = "all";
        public const string NotCovered = "not-covered";
        public const string HasSuggestions = "has-suggestions";
        public const string NotApplicable = "not-applicable";
        public const string NeedsReview = "needs-review";

        // In the order they are shown, with the text on the button
        public static readonly IReadOnlyList<(string Slug, string Title)> Each =
        [
            (All, "All"),
            (NotCovered, "Not covered"),
            (HasSuggestions, "Has suggestions"),
            (NotApplicable, "Not applicable"),
            (NeedsReview, "Needs review")
        ];

        // An unknown or missing filter shows everything
        public static string Find(string? slug)
            => Each.Select(f => f.Slug).FirstOrDefault(f => f == slug?.ToLowerInvariant()) ?? All;

        public static bool Matches(string filter, RequirementCoverage row) => filter switch
        {
            NotCovered => row.IsApplicable && !row.IsCovered, // not applicable is not "not covered" (US-043 R6)
            HasSuggestions => row.HasSuggestions,
            NotApplicable => !row.IsApplicable,
            NeedsReview => row.NeedsReview,
            _ => true
        };
    }

    // US-041: short names and addresses for the Compliance tab's sub-tabs.
    // The built-in frameworks are called e.g. "CRA – Regulation (EU) 2024/2847": the part before " – " is the short name.
    public static class FrameworkNames
    {
        public static string Short(string name) => Application.Frameworks.FrameworkSlugs.Short(name);

        // "Security policy" -> "security-policy" (US-058: also the start of a requirement's test tag)
        public static string Slug(string name) => Application.Frameworks.FrameworkSlugs.Slug(name);
    }

    // One framework a project follows, with the version it uses (US-011 R6) and who added it (US-022 R4)
    // Today: the day the page is shown for, from the app's clock (US-035)
    public record ProjectFrameworkRow(Framework Framework, FrameworkVersion Version, ProjectFramework Link, DateOnly Today)
    {
        // US-007: only an activated version counts as newer; drafts cannot be used yet
        public bool NewerVersionExists => Framework.CurrentVersion(Today) is { } current && current.Id != Version.Id
            && current.Number > Version.Number;

        // US-035 R5: a newer version that does not apply yet, which the project can move to early to prepare
        public FrameworkVersion? UpcomingToPrepareFor
            => Framework.UpcomingVersion(Today) is { } upcoming && upcoming.Number > Version.Number ? upcoming : null;

        // US-022 R4, US-035 R3, R6
        public string Status
        {
            get
            {
                // US-069: e.g. CRA version 1 (applies from 11-12-2027) after version 2 started applying earlier
                if (NewerVersionExists)
                {
                    return "Newer version available";
                }

                if (Version.IsUpcomingOn(Today))
                {
                    var from = Version.AppliesFrom!.Value.ToString("dd-MM-yyyy");
                    return Framework.CurrentVersion(Today) == Version ? $"Applies from {from}" : $"Upcoming (applies from {from})";
                }

                return "Newest";
            }
        }
    }

    // The project's user stories from GitHub and how the last sync went (Views/Projects/_WorkItems.cshtml, US-018)
    // Compliance: when given, each user story shows the requirements it is linked to (US-021 R3)
    // Documents: the requirement specifications the document work items come from (US-032)
    public record WorkItemsViewModel(
        Project Project, IReadOnlyList<WorkItem> WorkItems, ProjectCompliance? Compliance = null, IReadOnlyList<RequirementDocument>? Documents = null)
    {
        // US-104 R3: only the items of the methods the project uses; the others are kept and still count (R5)
        public IReadOnlyList<WorkItem> VisibleWorkItems
            => WorkItems.Where(item => item.Source switch
            {
                WorkItemSource.GitHub => Project.RequirementMethods.HasFlag(RequirementMethods.GitHubUserStories),
                _ => Documents?.FirstOrDefault(d => d.Id == item.DocumentId) is not { } document
                     || Project.RequirementMethods.HasFlag(document.Kind == DocumentKind.UseCases
                         ? RequirementMethods.UseCases
                         : RequirementMethods.RequirementSpecification)
            }).ToList();

        // US-032: e.g. "kravspec.xlsx", for a requirement read from a document
        public string DocumentName(WorkItem item)
            => Documents?.FirstOrDefault(d => d.Id == item.DocumentId)?.Name ?? "Document";

        // US-032 R5: a document's requirement is "Active" or "Removed in version N"; GitHub's keep their own state
        public static string StateText(WorkItem item) => item.Source switch
        {
            WorkItemSource.Document when item.State == WorkItemState.Removed => $"Removed in version {item.RemovedInVersion}",
            WorkItemSource.Document => "Active",
            _ => item.State.ToString()
        };
    }

    // Data for the project page (Views/Projects/Details.cshtml and its tabs)
    public record ProjectDetailsViewModel(Project Project, IReadOnlyList<Framework> AvailableFrameworks, IReadOnlyList<WorkItem> WorkItems, DateOnly Today)
    {
        // US-022: the tab being shown (ProjectTabs), the organisation (for names) and the Activity tab's log entries
        public string Tab { get; init; } = ProjectTabs.Overview;
        public Organization? Organization { get; init; }
        public IReadOnlyList<ActivityLogEntry> Activity { get; init; } = [];

        // US-101: the Activity tab's page of entries, the filters behind it, and filters that were ignored (R9)
        public Application.ActivityLog.ActivityLogPage? ActivityPage { get; init; }
        public Application.ActivityLog.ActivityLogQuery ActivityQuery { get; init; } = new();
        public IReadOnlyList<string> ActivityIgnored { get; init; } = [];

        // US-019, US-021, US-041: the Compliance tab's data, the chosen sub-tab (slug), the search and the filter
        public ProjectCompliance? Compliance { get; init; }

        // US-059: the Evidence tab, and the project's evidence for "Pick existing evidence" on the Compliance tab
        public EvidenceTab? EvidenceTab { get; init; }
        public string? Sub { get; init; }
        public string? Search { get; init; }
        public string ComplianceFilter { get; init; } = ComplianceFilters.All;

        // US-042: the Risks tab's register (already filtered), the filters and the time for "Review due"
        public IReadOnlyList<Risk> Risks { get; init; } = [];
        public IReadOnlyList<Risk> AllRisks { get; init; } = [];

        // US-026: each built-in regulation the project follows, with its class (Overview tab)
        public IReadOnlyList<Application.Projects.RegulationClassification> Classifications { get; init; } = [];
        // US-073: the project's security events (CRA Art. 14), for their tab and the Overview's deadlines
        public IReadOnlyList<Domain.Security.SecurityEvent> SecurityEvents { get; init; } = [];

        // US-128: suggested security events and the exploited lists' status
        public Application.Security.SuggestionOverview? EventSuggestions { get; init; }

        // US-135: the project's approval requests, newest first
        public IReadOnlyList<Application.Approvals.ApprovalView> Approvals { get; init; } = [];

        // US-078: the Scans tab, and the Overview's count of open high and critical findings
        public Application.Scanning.ScanOverview? Scans { get; init; }

        // US-107 R3: the organisation's GitHub Projects to choose from
        public IReadOnlyList<Application.WorkItems.GitHubProjectInfo> GitHubProjectChoices { get; init; } = [];
        public string? RiskStatusFilter { get; init; }
        public string? RiskLevelFilter { get; init; }
        public bool OnlyReviewDue { get; init; }
        public DateTime Now { get; init; }

        // The framework of the chosen sub-tab, or the first one (R3)
        public FrameworkCompliance? ActiveCompliance
            => Compliance?.Frameworks.FirstOrDefault(f => FrameworkNames.Slug(f.Framework.Name) == Sub)
               ?? Compliance?.Frameworks.FirstOrDefault();

        // US-032: the project's requirement specifications with their versions (User stories tab)
        public IReadOnlyList<RequirementDocument> Documents { get; init; } = [];

        public WorkItemsViewModel WorkItemsSection => new(Project, WorkItems, Compliance, Documents);

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
                return framework is null || version is null ? null : new ProjectFrameworkRow(framework, version, link, Today);
            })
            .OfType<ProjectFrameworkRow>()
            .OrderBy(r => r.Framework.Name)
            .ToList();

        // The frameworks that can still be added (the project uses each framework at most once, R3),
        // and that have an active version (US-007)
        public IEnumerable<Framework> CanBeAdded
            => AvailableFrameworks.Where(f => Project.FrameworkInUse(f.Id) is null && f.CurrentVersion(Today) is not null);
    }

    // One line in the organisation overview: a framework version and the projects using it (US-011 R5)
    public record FrameworkUsageRow(Framework Framework, FrameworkVersion Version, IReadOnlyList<Project> Projects, DateOnly Today)
    {
        public bool IsNewest => Version.Id == Framework.CurrentVersion(Today)?.Id;
    }

    // Data for the organisation's Overview tab: the organisation, its projects and which frameworks they use
    public record OrganizationOverviewViewModel(
        Organization Organization,
        IReadOnlyList<Project> Projects,
        IReadOnlyList<Framework> Frameworks,
        DateOnly Today)
    {
        // US-096: the "Get started" card, or null
        public Application.Organizations.GetStartedChecklist? GetStarted { get; init; }

        // Per framework: the newest version (even with no projects yet), then every older version in use
        public IReadOnlyList<FrameworkUsageRow> FrameworkUsage { get; } = Frameworks
            .SelectMany(framework => framework.Versions
                .OrderByDescending(v => v.Number)
                .Select(version => new FrameworkUsageRow(
                    framework,
                    version,
                    Projects.Where(p => p.Frameworks.Any(f => f.FrameworkVersionId == version.Id)).ToList(),
                    Today))
                .Where(row => row.IsNewest || row.Projects.Count > 0))
            .ToList();
    }
}
