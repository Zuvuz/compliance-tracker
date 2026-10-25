using ComplianceTracker.Domain.Organizations;
using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Models
{
    // Formularens felter. Ingen valideringsregler her: dem håndhæver domænet (R6).
    // Felterne er nullable, så MVC ikke selv laver "required"-fejl, men lader domænet svare.
    public class CreateOrganizationViewModel
    {
        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Display(Name = "CVR")]
        public string? Cvr { get; set; }

        [Display(Name = "Sector")]
        public Sector? Sector { get; set; }

        [Display(Name = "Size")]
        public OrganizationSize Size { get; set; }

        // Ingen "Your name": den, der opretter, er den indloggede bruger (US-004 R3)
    }
}
