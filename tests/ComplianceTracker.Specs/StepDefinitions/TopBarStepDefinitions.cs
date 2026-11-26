using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-225: the top bar (#top-bar a.nav-link) and the rows of subpages under it: #compliance-nav, #organisation-nav and
    // #isms-nav; a project's own tabs are #project-groups.
    [Binding]
    public class TopBarStepDefinitions
    {
        private static readonly string[] Rows = ["#compliance-nav", "#organisation-nav", "#isms-nav", "#project-groups"];
        private readonly WebScenario _web;
        private readonly List<(string Path, int Rows, List<string> Names)> _visited = [];

        public TopBarStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the top bar has exactly {string}, {string}, {string}, {string} and {string}")]
        public void ThenExactly(string a, string b, string c, string d, string e)
            => Assert.Equal([a, b, c, d, e], _web.Page!.QuerySelectorAll("#top-bar a.nav-link").Select(l => l.TextContent.Trim()).ToList());

        [When("I open every subpage of {string}, {string} and {string}")]
        public async Task WhenIOpenEverySubpage(string first, string second, string third)
        {
            foreach (var item in new[] { first, second, third })
            {
                await _web.OpenAsync("/Projects");
                var top = _web.Page!.QuerySelectorAll("#top-bar a.nav-link").FirstOrDefault(a => a.TextContent.Trim() == item)
                          ?? throw new Xunit.Sdk.XunitException($"No '{item}' in the top bar.");
                await _web.OpenAsync(top.GetAttribute("href")!);
                var links = Rows.SelectMany(r => _web.Page!.QuerySelectorAll($"{r} a.nav-link")).Select(a => a.GetAttribute("href")!).Distinct().ToList();
                Assert.NotEmpty(links);
                foreach (var href in links)
                {
                    await _web.OpenAsync(href);
                    var rows = Rows.Where(r => _web.Page!.QuerySelector(r) is not null).ToList();
                    var names = rows.SelectMany(r => _web.Page!.QuerySelectorAll($"{r} a.nav-link")).Select(a => a.TextContent.Trim()).ToList();
                    _visited.Add((href, rows.Count, names));
                }
            }
        }

        [Then("no page has more than one row of subpages")]
        public void ThenOneRow()
            => Assert.All(_visited, v => Assert.True(v.Rows <= 1, $"{v.Path} has {v.Rows} rows of subpages"));

        [Then("no row has two subpages with the same name")]
        public void ThenUniqueNames()
            => Assert.All(_visited, v => Assert.True(v.Names.Count == v.Names.Distinct().Count(), $"{v.Path}: {string.Join(", ", v.Names)}"));

        [Then("the Compliance page {string} is marked")]
        public void ThenMarked(string text)
            => Assert.Equal([text], _web.Page!.QuerySelectorAll("#compliance-nav a.nav-link.active").Select(a => a.TextContent.Trim()).ToList());

        [Then("the page has no project tabs")]
        public void ThenNoProjectTabs() => Assert.Null(_web.Page!.QuerySelector("#project-groups"));
    }
}
