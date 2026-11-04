using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-064: the theme and the product name, through the web app with test sign-in.
    // UI contract: Views/Shared/_Layout.cshtml (nav.ct-topbar, #product-name, link to css/site.css)
    // and wwwroot/css/site.css (the @font-face points at the tracker's own fonts folder).
    [Binding]
    public class ThemeStepDefinitions
    {
        private readonly WebScenario _web;
        private readonly DatabaseScenario _database;
        private readonly ScenarioClock _clock;

        public ThemeStepDefinitions(WebScenario web, DatabaseScenario database, ScenarioClock clock)
        {
            _web = web;
            _database = database;
            _clock = clock;
        }

        // A separate app with the setting, as the product name is read when the app starts
        [When("I open the front page with the product name set to {string}")]
        public async Task WhenIOpenTheFrontPageWithTheProductName(string name)
        {
            using var app = TestApp.WithTestSignIn(_database.ConnectionString, _clock,
                new Dictionary<string, string?> { ["Branding:ProductName"] = name });
            using var client = app.Factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "Anna");
            _web.Response = await client.GetAsync("/");
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        [Then("the page uses the stylesheet {string}")]
        public void ThenThePageUsesTheStylesheet(string file)
            => Assert.Contains(Stylesheets(), href => href.Split('?')[0].EndsWith("/" + file, StringComparison.Ordinal));

        [Then("the top bar has the theme's class")]
        public void ThenTheTopBarHasTheThemesClass()
            => Assert.NotNull(_web.Page!.QuerySelector("nav.ct-topbar #top-bar"));

        // R6: every stylesheet and script is a path on the tracker, and the theme's fonts are too
        [Then("every stylesheet, script and font comes from the tracker itself")]
        public async Task ThenEverythingComesFromTheTrackerItself()
        {
            var addresses = Stylesheets()
                .Concat(_web.Page!.QuerySelectorAll("script[src]").Select(s => s.GetAttribute("src")!))
                .ToList();
            Assert.NotEmpty(addresses);
            Assert.All(addresses, address => Assert.StartsWith("/", address));
            Assert.DoesNotContain(addresses, address => address.StartsWith("//", StringComparison.Ordinal));

            var css = await _web.Client.GetStringAsync(Stylesheets().Single(h => h.Contains("/css/site.css")));
            Assert.DoesNotContain("http", css, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("../fonts/inter/inter-latin-wght-normal.woff2", css);

            var font = await _web.Client.GetAsync("/fonts/inter/inter-latin-wght-normal.woff2");
            Assert.True(font.IsSuccessStatusCode, $"The font is not served: {font.StatusCode}");
        }

        [Then("the page title ends with {string}")]
        public void ThenThePageTitleEndsWith(string name)
            => Assert.EndsWith(name, _web.Page!.Title?.Trim());

        [Then("the top bar shows {string}")]
        public void ThenTheTopBarShows(string name)
            => Assert.Equal(name, _web.Page!.QuerySelector("#product-name")?.TextContent.Trim());

        private IReadOnlyList<string> Stylesheets()
            => _web.Page!.QuerySelectorAll("link[rel=stylesheet]").Select(l => l.GetAttribute("href")!).ToList();
    }
}
