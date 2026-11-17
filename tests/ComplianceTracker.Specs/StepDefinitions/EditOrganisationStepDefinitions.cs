using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-027: Views/Organisation/Profile.cshtml (form#edit-organisation with name, sector and size; #profile-cvr;
    // #profile-read-only) and the Organisation menu's #nav-organisation-profile.
    [Binding]
    public class EditOrganisationStepDefinitions
    {
        private readonly WebScenario _web;

        public EditOrganisationStepDefinitions(WebScenario web) => _web = web;

        [When("I open the organisation's profile")]
        public async Task WhenIOpenTheProfile()
        {
            await _web.OpenAsync("/Projects");
            var link = _web.Page!.QuerySelector("#nav-organisation-profile") ?? throw new Xunit.Sdk.XunitException("The Organisation menu has no Profile.");
            await _web.OpenAsync(link.GetAttribute("href")!);
        }

        [When("I change the size of the organisation to {string}")]
        public Task WhenIChangeTheSize(string size) => SaveAsync(size: size);

        [When("I change the name of the organisation to {string}")]
        public Task WhenIChangeTheName(string name) => SaveAsync(name: name);

        // Sends the form with its current values and the one change
        private async Task SaveAsync(string? name = null, string? size = null)
        {
            await WhenIOpenTheProfile();
            var form = _web.Page!.QuerySelector("form#edit-organisation") ?? throw new Xunit.Sdk.XunitException("The profile has no form.");
            var values = new Dictionary<string, string>
            {
                ["name"] = name ?? form.QuerySelector("input[name=name]")!.GetAttribute("value")!,
                ["sector"] = form.QuerySelector("select[name=sector] option[selected]")!.GetAttribute("value")!,
                ["size"] = size ?? form.QuerySelector("select[name=size] option[selected]")!.GetAttribute("value")!
            };
            await _web.PostFormAsync(form.GetAttribute("action")!, values);
        }

        [Then("the profile shows the size {string}")]
        public async Task ThenTheProfileShowsTheSize(string size)
        {
            await WhenIOpenTheProfile();
            Assert.Equal(size, _web.Page!.QuerySelector("form#edit-organisation select[name=size] option[selected]")?.GetAttribute("value"));
        }

        [Then("the profile shows the name {string}")]
        public async Task ThenTheProfileShowsTheName(string name)
        {
            await WhenIOpenTheProfile();
            Assert.Equal(name, _web.Page!.QuerySelector("form#edit-organisation input[name=name]")?.GetAttribute("value"));
        }

        [Then("the CVR is shown but cannot be edited")]
        public void ThenTheCvrCannotBeEdited()
        {
            Assert.False(string.IsNullOrWhiteSpace(_web.Page!.QuerySelector("#profile-cvr")?.TextContent));
            Assert.Null(_web.Page!.QuerySelector("form#edit-organisation [name=cvr]"));
        }

        [Then("the profile has no form")]
        public void ThenTheProfileHasNoForm()
        {
            Assert.Null(_web.Page!.QuerySelector("form#edit-organisation"));
            Assert.NotNull(_web.Page!.QuerySelector("#profile-read-only"));
        }

        // R3: a direct request is refused on the server too, and nothing changes
        [Then("changing the size anyway is refused")]
        public async Task ThenChangingAnywayIsRefused()
        {
            await _web.PostFormAsync("/Organisation/Update", new Dictionary<string, string> { ["name"] = "Acme ApS", ["sector"] = "Other", ["size"] = "Large" });
            await using var db = _web.CreateDbContext();
            Assert.NotEqual(Domain.Organizations.OrganizationSize.Large, (await db.Organizations.SingleAsync(o => o.Name == "Acme ApS")).Size);
        }
    }
}
