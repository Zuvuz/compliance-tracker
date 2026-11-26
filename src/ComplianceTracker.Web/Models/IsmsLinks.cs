using Microsoft.AspNetCore.Mvc;

namespace ComplianceTracker.Web.Models
{
    // US-216, US-227: the page an ISMS item is done on. The ISMS overview names pages as "Scope", "Documents",
    // "risk:{id}" and so on; Home and the ISMS overview turn them into the same links.
    public static class IsmsLinks
    {
        public static string Href(IUrlHelper url, string page, string? fragment = null)
        {
            var href = page switch
            {
                "Documents" => url.Action("Index", "Documents"),
                "Projects" => url.Action("Index", "Projects"),
                "Frameworks" => url.Action("Index", "Frameworks"),
                _ when page.StartsWith("risk:", StringComparison.Ordinal) => url.Action("Details", "Risks", new { id = page[5..] }),
                _ => url.Action(page, "Isms")
            } ?? "#";
            return fragment is null ? href : $"{href}#{fragment}";
        }
    }
}
