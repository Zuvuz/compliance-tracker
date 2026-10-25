using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Webtests af opret-formularen (US-003). Webappen kører i hukommelsen med test-login,
    // og databasen er PostgreSQL i Testcontainers. De fleste "Then"-steps her bruges også af US-004.
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
            // Oprettet af en anden bruger end den indloggede
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
            // En indlogget bruger, men uden antiforgery-cookie og -token,
            // som et forfalsket request fra et andet website
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
        public void ThenIsShownAsAdmin(string userName)
        {
            Assert.Contains(UserRows(), cells => cells[0] == userName && cells[2] == "Admin");
        }

        [Then("{string} with e-mail {string} is shown as Admin")]
        public void ThenWithEmailIsShownAsAdmin(string userName, string email)
        {
            Assert.Contains(UserRows(), cells => cells[0] == userName && cells[1] == email && cells[2] == "Admin");
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

        // Rækkerne i brugertabellen på detaljesiden: navn, e-mail, rolle
        private IEnumerable<string[]> UserRows()
            => _web.Page!.QuerySelectorAll("#organisation-users tbody tr")
                .Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToArray());
    }
}
