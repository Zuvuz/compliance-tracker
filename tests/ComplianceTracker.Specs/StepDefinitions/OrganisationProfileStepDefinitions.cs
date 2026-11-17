using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-163: Views/Organisation/Profile.cshtml (#suggested-industry, form#edit-organisation select[name=sector],
    // #organisation-country, #industry-profile with form#choose-industry, #profile-industry) and Views/Settings/Index.cshtml
    // (#settings-for, #settings-to-profile).
    [Binding]
    public class OrganisationProfileStepDefinitions
    {
        private readonly WebScenario _web;

        public OrganisationProfileStepDefinitions(WebScenario web) => _web = web;

        // R2: the page is opened with the sector being chosen, as the select does in the browser; nothing is saved
        [When("I choose the NIS2 sector {string}")]
        public async Task WhenIChooseTheSector(string sector)
        {
            var select = _web.Page!.QuerySelector("form#edit-organisation select[name=sector]") ?? throw new Xunit.Sdk.XunitException("The profile has no sector.");
            var option = select.QuerySelectorAll("option").SingleOrDefault(o => o.GetAttribute("value") == sector)
                ?? throw new Xunit.Sdk.XunitException($"No sector '{sector}'.");
            Assert.False(string.IsNullOrEmpty(option.GetAttribute("data-suggests")), "The sector suggests no industry in the browser.");
            await _web.OpenAsync($"/Organisation/Profile?sector={Uri.EscapeDataString(sector)}");
        }

        [Then("the profile suggests the industry {string}")]
        public void ThenTheProfileSuggests(string industry)
            => Assert.Equal($"Suggested industry: {industry}", _web.Page!.QuerySelector("#suggested-industry")?.TextContent.Trim());

        [Then("the organisation's sector is still {string}")]
        public async Task ThenTheSectorIsStill(string sector)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(sector, (await db.Organizations.SingleAsync()).Sector.ToString());
        }

        [Then("the profile has the country, the industry and {string}, each saying what it changes")]
        public void ThenTheProfileHasTheRest(string ai)
        {
            var page = _web.Page!;
            Assert.Contains("national laws", page.QuerySelector("#organisation-country")?.TextContent);
            Assert.NotNull(page.QuerySelector("form#choose-industry select[name=industry]"));
            Assert.Contains(ai, page.QuerySelector("form#choose-industry label[for=products-use-ai]")?.TextContent);
            Assert.Contains("decides what the tracker suggests", page.QuerySelector("#industry-profile")?.TextContent);
            Assert.Contains("decides whether NIS2 applies", page.QuerySelector("#profile-intro")?.TextContent);
        }

        [Then("the industry list has {string}, {string}, {string} and {string}")]
        public void ThenTheIndustryListHas(string first, string second, string third, string fourth)
        {
            var values = _web.Page!.QuerySelectorAll("form#choose-industry select[name=industry] option")
                .Select(o => o.GetAttribute("value")).Where(v => !string.IsNullOrEmpty(v));
            Assert.Equal([first, second, third, fourth], values);
        }

        [Then("{string} says its own suggestions are coming")]
        public void ThenSaysComing(string industry)
        {
            var option = _web.Page!.QuerySelectorAll("form#choose-industry select[name=industry] option").Single(o => o.GetAttribute("value") == industry);
            Assert.EndsWith("(own suggestions coming)", option.TextContent.Trim());
        }

        [Then("the Settings page says it is for the organisation's admins")]
        public void ThenSettingsSaysWhoFor()
            => Assert.StartsWith("For the organisation's admins.", _web.Page!.QuerySelector("#settings-for")?.TextContent.Trim());

        [Then("the Settings page has no industry or country choice")]
        public void ThenSettingsHasNoIndustry()
        {
            Assert.Null(_web.Page!.QuerySelector("form#choose-industry"));
            Assert.Null(_web.Page!.QuerySelector("form#set-country"));
            Assert.Null(_web.Page!.QuerySelector("#industry-profile"));
        }

        [Then("the Settings page links to the organisation's profile")]
        public void ThenSettingsLinksToProfile()
            => Assert.Equal("/Organisation/Profile", _web.Page!.QuerySelector("#settings-to-profile")?.GetAttribute("href"));

        [Then("the profile shows the industry {string}")]
        public void ThenTheProfileShowsTheIndustry(string industry)
        {
            Assert.Equal(industry, _web.Page!.QuerySelector("#profile-industry")?.TextContent.Trim());
            Assert.Null(_web.Page!.QuerySelector("form#choose-industry"));
            Assert.Null(_web.Page!.QuerySelector("form#set-country"));
        }

        // A page opened with another member's own client (NavigationStepDefinitions), read from the answer
        [Then("the answer is the page and says {string}")]
        public async Task ThenTheAnswerSays(string text)
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, _web.Response!.StatusCode);
            Assert.Contains(text, await _web.Response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }
}
