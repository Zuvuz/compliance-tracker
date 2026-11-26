using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-226: the ISMS row (#isms-nav a.nav-link, the open page .active) and the Documents page's filter
    // (#document-filter a[data-kind], #organisation-documents li[data-title]).
    [Binding]
    public class IsmsInOnePlaceStepDefinitions
    {
        private readonly WebScenario _web;

        public IsmsInOnePlaceStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Then("the ISMS row is {string}, {string}, {string}, {string}, {string}, {string}, {string}, {string}, {string}, {string}, {string}, {string}")]
        public void ThenTheRowIs(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k, string l)
            => Assert.Equal([a, b, c, d, e, f, g, h, i, j, k, l], Links());

        [When("I open the ISMS page {string}")]
        public async Task WhenIOpen(string text)
        {
            await _web.OpenAsync("/Isms");
            var link = _web.Page!.QuerySelectorAll("#isms-nav a.nav-link").FirstOrDefault(a => a.TextContent.Trim() == text)
                       ?? throw new Xunit.Sdk.XunitException($"The ISMS row has no '{text}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [Then("the ISMS page {string} is marked")]
        public void ThenMarked(string text)
            => Assert.Equal([text], _web.Page!.QuerySelectorAll("#isms-nav a.nav-link.active").Select(a => a.TextContent.Trim()).ToList());

        [Then("the document filter {string} is chosen")]
        public void ThenFilter(string title)
            => Assert.StartsWith(title, _web.Page!.QuerySelector("#document-filter a.active")?.TextContent.Trim() ?? "", StringComparison.Ordinal);

        [Then("the documents list {string}")]
        public void ThenList(string title)
            => Assert.NotNull(_web.Page!.QuerySelector($"#organisation-documents li[data-title='{title}']"));

        private List<string> Links() => _web.Page!.QuerySelectorAll("#isms-nav a.nav-link").Select(a => a.TextContent.Trim()).ToList();
    }
}
