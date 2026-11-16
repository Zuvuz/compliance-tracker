using AngleSharp.Dom;

namespace ComplianceTracker.Specs.Support
{
    // #52: a project's tab groups are links to their first tab, and only the current group's tabs are shown.
    // Like a user, this opens each group to see every tab (title and address); the page shown last is the last group.
    public static class ProjectNav
    {
        public static async Task<IReadOnlyList<(string Title, string Href)>> AllTabsAsync(WebScenario web)
        {
            var tabs = Tabs(web).ToList();
            var groups = web.Page!.QuerySelectorAll("#project-groups a[data-group]").Select(a => a.GetAttribute("href")!).Distinct().ToList();
            foreach (var group in groups)
            {
                await web.OpenAsync(group);
                tabs.AddRange(Tabs(web));
            }
            return tabs.DistinctBy(t => t.Title).ToList();
        }

        private static IEnumerable<(string Title, string Href)> Tabs(WebScenario web)
            => web.Page!.QuerySelectorAll("#project-nav a[data-tab]").Select(a => (a.TextContent.Trim(), a.GetAttribute("href")!));
    }
}
