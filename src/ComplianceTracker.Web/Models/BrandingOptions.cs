namespace ComplianceTracker.Web.Models
{
    // US-064 R7: the product's name, set in one place (Branding:ProductName), so the final name can be chosen later.
    // Shown in the page title, the top bar and the footer.
    public class BrandingOptions
    {
        public const string Section = "Branding";
        public const string DefaultProductName = "Compliance Tracker";

        public string ProductName { get; set; } = DefaultProductName;

        // An empty setting falls back to the default, so the top bar is never blank
        public string Name => string.IsNullOrWhiteSpace(ProductName) ? DefaultProductName : ProductName.Trim();
    }
}
