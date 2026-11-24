using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-045: the top bar and the pages it links to, through the web app with test sign-in.
    // UI contract: Views/Shared/_Layout.cshtml (#top-bar with a.nav-link), the organisation pages have no .nav-tabs.
    // #52: Views/Shared/_OrganisationNav.cshtml (#organisation-nav with a.nav-link, the open page has .active).
    [Binding]
    public class NavigationStepDefinitions
    {
        private readonly WebScenario _web;

        public NavigationStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- When

        [When("{string} opens the front page")]
        public async Task WhenOpensTheFrontPage(string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            _web.Response = await client.GetAsync("/");
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        // Without following redirects, so "access is denied" can see the answer itself
        [When("{string} opens the address {string}")]
        public async Task WhenOpensTheAddress(string userName, string path)
        {
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            _web.Response = await client.GetAsync(path);
        }

        [When("{string} opens the organisation's pages")]
        public async Task WhenOpensTheOrganisationsPages(string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            _web.Response = await client.GetAsync("/Organisation");
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        [When("I open the organisation's page {string}")]
        public async Task WhenIOpenTheOrganisationsPage(string text)
        {
            await _web.OpenAsync("/Organisation");
            var link = _web.Page!.QuerySelectorAll("#organisation-nav a.nav-link").FirstOrDefault(a => a.TextContent.Trim() == text)
                ?? throw new Xunit.Sdk.XunitException($"The organisation's pages have no link '{text}'.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I open the old users address of {string}")]
        public async Task WhenIOpenTheOldUsersAddress(string organisation)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Organizations.Where(o => o.Name == organisation).Select(o => o.Id).SingleAsync();
            await _web.OpenAsync($"/Organizations/Users/{id}");
        }

        // ---------------------------------------------------------- Then

        [Then("the top bar has the links {string}, {string}, {string} and {string}")]
        public void ThenTheTopBarHasFourLinks(string first, string second, string third, string fourth)
            => AssertLinks(first, second, third, fourth);

        [Then("the top bar has the links {string} and {string}")]
        public void ThenTheTopBarHasTwoLinks(string first, string second)
            => AssertLinks(first, second);

        [Then("the top bar has no link {string}")]
        public void ThenTheTopBarHasNoLink(string text)
            => Assert.DoesNotContain(text, TopBarLinks());

        [Then("the organisation's pages have the links {string} and {string}")]
        public void ThenTheOrganisationsPagesHaveTheLinks(string first, string second)
        {
            var links = OrganisationLinks();
            Assert.Contains(first, links);
            Assert.Contains(second, links);
        }

        [Then("the organisation's pages have no link {string}")]
        public void ThenTheOrganisationsPagesHaveNoLink(string text)
            => Assert.DoesNotContain(text, OrganisationLinks());

        [Then("the top bar opens no menu")]
        public void ThenTheTopBarOpensNoMenu()
            => Assert.Empty(_web.Page!.QuerySelectorAll("#top-bar .dropdown-menu, #top-bar [data-bs-toggle='dropdown']")
                .Where(e => e.Closest("#organisation-switcher") is null));

        [Then("the organisation's page {string} is marked")]
        public void ThenTheOrganisationsPageIsMarked(string text)
        {
            var marked = _web.Page!.QuerySelectorAll("#organisation-nav a.nav-link.active").Select(a => a.TextContent.Trim()).ToList();
            Assert.Equal([text], marked);
        }

        [Then("the page has no tab {string}")]
        public void ThenThePageHasNoTab(string text)
            => Assert.DoesNotContain(text, _web.Page!.QuerySelectorAll(".nav-tabs a").Select(a => a.TextContent.Trim()));

        [Then("I am on the page {string}")]
        public void ThenIAmOnThePage(string path)
            => Assert.Equal(path, _web.CurrentPath);

        // ---------------------------------------------------------- Helpers

        private void AssertLinks(params string[] expected)
        {
            var links = TopBarLinks();
            foreach (var text in expected)
            {
                Assert.Contains(text, links);
            }
        }

        private List<string> TopBarLinks()
            => _web.Page!.QuerySelectorAll("#top-bar a.nav-link").Select(a => a.TextContent.Trim()).ToList();

        // #52: the organisation's pages are a row of links on the organisation's pages, not a dropdown in the top bar
        private List<string> OrganisationLinks()
            => _web.Page!.QuerySelectorAll("#organisation-nav a.nav-link").Select(a => a.TextContent.Trim()).ToList();
    }
}
