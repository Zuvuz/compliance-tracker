using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Models
{
    // The fields of the create-project form (US-008). No validation rules here: the domain enforces them.
    // The text fields are nullable, so MVC does not create its own "required" errors but lets the domain answer.
    public class CreateProjectViewModel
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
    }

    // Data for the organisation's Overview tab: the organisation and its projects
    public record OrganizationOverviewViewModel(Organization Organization, IReadOnlyList<Project> Projects);
}
