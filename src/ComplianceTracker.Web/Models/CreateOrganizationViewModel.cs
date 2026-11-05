using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Web.Filters;
using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Models
{
    // The fields of the create form. No validation rules here: the domain enforces them (US-003 R6).
    // The fields are nullable, so MVC does not create its own "required" errors but lets the domain answer.
    // IFormModel: on a broken rule, DomainExceptionFilter shows the form again with what the user typed (US-003 R3)
    public class CreateOrganizationViewModel : IFormModel
    {
        // [Display] sets the label text shown next to the field
        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Display(Name = "CVR")]
        public string? Cvr { get; set; }

        [Display(Name = "Sector")]
        public Sector? Sector { get; set; }

        [Display(Name = "Size")]
        // Nullable like Sector: the form starts with no size chosen, and the domain says "Size is required"
        public OrganizationSize? Size { get; set; }

        // No "Your name": the creator is the signed-in user (US-004 R3)
    }
}
