using AngleSharp.Dom;

namespace ComplianceTracker.Specs.Support
{
    // US-153: a requirement's details and actions are on its own page; the Compliance tab only lists light rows.
    // Find returns the requirement's page (article.requirement-row[data-detail]) the way a user gets there:
    // - already on that page: the page;
    // - on the overview: clicks the row's link;
    // - on another requirement's page: goes back to the overview first (and to "All" when the row is in another framework).
    // Null when the requirement is not on the overview at all.
    public static class RequirementRowLookup
    {
        // Sync steps call this; Task.Run keeps the wait off xUnit's synchronisation context, so it cannot deadlock
        public static IElement? Find(WebScenario web, string code)
            => Task.Run(() => FindAsync(web, code)).GetAwaiter().GetResult();

        public static async Task<IElement?> FindAsync(WebScenario web, string code)
        {
            if (Page(web, code) is { } page)
            {
                return page;
            }

            if (web.Page?.QuerySelector("#back-to-requirements") is { } back)
            {
                await web.OpenAsync(back.GetAttribute("href")!);
            }

            var row = ListRow(web, code);
            if (row is null && web.Page?.QuerySelector("#compliance-tabs a[data-sub='all']") is { } all)
            {
                await web.OpenAsync(all.GetAttribute("href")!);
                row = ListRow(web, code);
            }

            if (row?.QuerySelector("a.requirement-open")?.GetAttribute("href") is not { } href)
            {
                return null;
            }

            await web.OpenAsync(href);
            return Page(web, code);
        }

        // The overview: from a requirement's page, its back link (the summary, coverage and filters are there)
        public static void Overview(WebScenario web) => Task.Run(() => OverviewAsync(web)).GetAwaiter().GetResult();

        public static async Task OverviewAsync(WebScenario web)
        {
            if (web.Page?.QuerySelector("#back-to-requirements") is { } back)
            {
                await web.OpenAsync(back.GetAttribute("href")!);
            }
        }

        // The AI's story suggestions of every row on the overview that has something waiting, read on each requirement's
        // page; the overview is shown again afterwards
        public static async Task<IReadOnlyList<IElement>> SuggestionsAsync(WebScenario web)
        {
            await OverviewAsync(web);
            var overview = web.Response!.RequestMessage!.RequestUri!.PathAndQuery;
            var pages = web.Page!.QuerySelectorAll("#requirements-table tr.requirement-row")
                .Where(r => r.QuerySelector(".row-waiting") is not null)
                .Select(r => r.QuerySelector("a.requirement-open")!.GetAttribute("href")!)
                .ToList();
            var found = new List<IElement>();
            foreach (var href in pages)
            {
                await web.OpenAsync(href);
                found.AddRange(web.Page!.QuerySelectorAll(".ai-suggestion"));
            }

            await web.OpenAsync(overview);
            return found;
        }

        // The light row on the overview
        public static IElement? ListRow(WebScenario web, string code)
            => web.Page?.QuerySelectorAll("#requirements-table tr.requirement-row").FirstOrDefault(r => r.GetAttribute("data-code") == code);

        private static IElement? Page(WebScenario web, string code)
            => web.Page?.QuerySelectorAll("article.requirement-row[data-detail='true']").FirstOrDefault(r => r.GetAttribute("data-code") == code);
    }
}
