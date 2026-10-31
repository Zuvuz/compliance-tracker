using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Security
{
    // The Keycloak settings as a typed class (Options pattern, US-029 R3).
    // Read from the section "Authentication:Keycloak" and checked when the app starts,
    // so a missing value stops the app at once with a message that names it,
    // instead of failing the first time someone tries to sign in.
    public class KeycloakOptions
    {
        public const string Section = "Authentication:Keycloak";

        // e.g. http://localhost:8180/realms/compliance-tracker (appsettings.Development.json)
        [Required(ErrorMessage = "Authentication:Keycloak:Authority is required")]
        public string Authority { get; set; } = "";

        [Required(ErrorMessage = "Authentication:Keycloak:ClientId is required")]
        public string ClientId { get; set; } = "";

        // Never in the repository (US-004 R7): only in the environment variable Authentication__Keycloak__ClientSecret
        [Required(ErrorMessage = "Authentication:Keycloak:ClientSecret is required. Set the environment variable Authentication__Keycloak__ClientSecret")]
        public string ClientSecret { get; set; } = "";
    }
}
