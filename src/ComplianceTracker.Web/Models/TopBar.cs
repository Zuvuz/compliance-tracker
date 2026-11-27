namespace ComplianceTracker.Web.Models
{
    // US-225: the five items of the top bar, and which one a page belongs to, so every page has one row of subpages
    public static class TopBar
    {
        public const string Home = "Home";
        public const string Projects = "Projects";
        public const string Compliance = "Compliance";
        public const string Isms = "Isms";
        public const string Organisation = "Organisation";

        // The organisation area (US-142) is a project under the hood: its risks are ISMS, the rest Compliance
        public static string? SectionOf(string? controller, string? action, bool organisationArea, string? areaTab)
        {
            if (organisationArea)
            {
                return areaTab == ProjectTabs.Risks || controller == "Risks" ? Isms : Compliance;
            }

            return controller switch
            {
                "Review" => Home,
                "Projects" or "Risks" => Projects,
                "Frameworks" or "GapAnalysis" or "Checklists" => Compliance,
                "Isms" or "Documents" or "Suppliers" => Isms, // US-226
                "Organisation" when action == "Index" => Compliance,
                "Organisation" or "Users" or "Activity" or "Settings" => Organisation,
                _ => null
            };
        }

        // US-225: the Compliance row's pages and the one that is open
        public static string ComplianceOpen(string? controller, bool organisationArea, string? areaTab)
            => organisationArea
                ? areaTab switch
                {
                    ProjectTabs.Frameworks => "Frameworks",
                    ProjectTabs.Compliance => "Requirements",
                    ProjectTabs.Link => "Link", // US-220
                    ProjectTabs.Evidence => "Evidence",
                    ProjectTabs.Reports => "Reports",
                    _ => "Nis2"
                }
                : controller switch
                {
                    "GapAnalysis" => "GapAnalysis",
                    "Checklists" => "Checklists",
                    _ => "Frameworks"
                };
    }
}
