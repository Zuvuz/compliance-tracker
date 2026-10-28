using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Web tests of the create form (US-003). The web app runs in memory with test sign-in,
    // and the database is PostgreSQL in Testcontainers. Most "Then" steps here are also used by US-004.
    [Binding]
    public class CreateOrganisationUiStepDefinitions
    {
        private const string CreatePath = "/Organizations/Create";

        private readonly WebScenario _web;

        public CreateOrganisationUiStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("I am signed in as {string}")]
        public void GivenIAmSignedInAs(string name)
        {
            _web.SignInAs(name);
        }

        [Given("I am on the create organisation page")]
        public async Task GivenIAmOnTheCreateOrganisationPage()
        {
            await _web.OpenAsync(CreatePath);
            _web.Response!.EnsureSuccessStatusCode();
        }

        [Given("an organisation with CVR {string} exists")]
        public async Task GivenAnOrganisationWithCvrExists(string cvr)
        {
            // Created by another user than the signed-in one, so only the CVR clashes
            await _web.CreateOrganisationAsync("Other", "Acme ApS", cvr);
        }

        // ---------------------------------------------------------- When

        [When("I submit the form with name {string}, CVR {string}, sector {string} and size {string}")]
        public async Task WhenISubmitTheForm(string name, string cvr, string sector, string size)
        {
            await _web.PostFormAsync(CreatePath, new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Cvr"] = cvr,
                ["Sector"] = sector,
                ["Size"] = size
            });
        }

        [When("I post the form without an antiforgery token")]
        public async Task WhenIPostTheFormWithoutAnAntiforgeryToken()
        {
            // A signed-in user, but without the antiforgery cookie and token,
            // like a forged request from another website
            using var attacker = _web.CreateClient(signedInAs: "Anna");

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = "Evil ApS",
                ["Cvr"] = "66666666",
                ["Sector"] = "Other",
                ["Size"] = "Small"
            });

            _web.Response = await attacker.PostAsync(CreatePath, form);
        }

        // ---------------------------------------------------------- Then

        [Then("I see the details of the organisation {string}")]
        public void ThenISeeTheDetailsOfTheOrganisation(string name)
        {
            Assert.True(_web.Response!.IsSuccessStatusCode);
            Assert.Equal(name, _web.Page!.QuerySelector("#organisation-name")?.TextContent.Trim());
        }

        [Then("{string} is shown as Admin")]
        public async Task ThenIsShownAsAdmin(string userName)
        {
            Assert.Contains(await UserRowsAsync(), cells => cells[0] == userName && cells[2] == "Admin");
        }

        [Then("{string} with e-mail {string} is shown as Admin")]
        public async Task ThenWithEmailIsShownAsAdmin(string userName, string email)
        {
            Assert.Contains(await UserRowsAsync(), cells => cells[0] == userName && cells[1] == email && cells[2] == "Admin");
        }

        [Then("I see the error {string}")]
        public void ThenISeeTheError(string message)
        {
            var errors = _web.Page!.QuerySelectorAll(".validation-summary-errors li")
                .Select(item => item.TextContent.Trim());

            Assert.Contains(message, errors);
        }

        [Then("the name field still contains {string}")]
        public void ThenTheNameFieldStillContains(string name)
        {
            Assert.Equal(name, _web.Page!.QuerySelector<IHtmlInputElement>("input[name='Name']")?.Value);
        }

        [Then("the request is rejected with status {int}")]
        public void ThenTheRequestIsRejectedWithStatus(int statusCode)
        {
            Assert.Equal(statusCode, (int)_web.Response!.StatusCode);
        }

        [Then("no organisation is saved")]
        public async Task ThenNoOrganisationIsSaved()
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.Organizations.AnyAsync());
        }

        // The rows of the users table: name, e-mail, role.
        // The users are on their own tab, so the test clicks "Users" on the organisation page first, like a user would.
        private async Task<IEnumerable<string[]>> UserRowsAsync()
        {
            var usersTab = _web.Page!.QuerySelector("a#users-tab")?.GetAttribute("href")
                ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no Users tab.");

            await _web.OpenAsync(usersTab);

            return _web.Page!.QuerySelectorAll("#organisation-users tbody tr")
                .Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToArray())
                .ToList();
        }
    }
}
