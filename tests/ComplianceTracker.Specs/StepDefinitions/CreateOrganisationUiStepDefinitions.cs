using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
using ComplianceTracker.Specs.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Integrationstests af web-UI'et. WebApplicationFactory starter den rigtige webapp i hukommelsen,
    // og testene sender HTTP-requests til den, som en browser ville gøre. Databasen er Testcontainers-Postgres.
    [Binding]
    public class CreateOrganisationUiStepDefinitions
    {
        private const string CreatePath = "/Organizations/Create";

        private readonly DatabaseScenario _database;
        private WebApplicationFactory<Program>? _baseFactory;
        private WebApplicationFactory<Program>? _factory;
        private HttpClient? _client;

        private HttpResponseMessage? _response;
        private IDocument? _page;

        public CreateOrganisationUiStepDefinitions(DatabaseScenario database)
        {
            _database = database;
        }

        // ---------------------------------------------------------- Hjælpere

        private WebApplicationFactory<Program> Factory
        {
            get
            {
                if (_factory is null)
                {
                    // Webappen får testdatabasens connection string i stedet for den fra miljøvariablen
                    _baseFactory = new WebApplicationFactory<Program>();
                    _factory = _baseFactory.WithWebHostBuilder(builder =>
                        builder.ConfigureAppConfiguration((_, config) =>
                            config.AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["ConnectionStrings:ComplianceTracker"] = _database.ConnectionString
                            })));
                }

                return _factory;
            }
        }

        // Klienten husker cookies, så antiforgery-cookien fra GET følger med POST'en
        private HttpClient Client => _client ??= Factory.CreateClient();

        private static async Task<IDocument> ParseAsync(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            return await context.OpenAsync(request => request.Content(html));
        }

        private ComplianceTrackerDbContext CreateDbContext() => TestDatabase.CreateContext(_database.ConnectionString);

        [AfterScenario]
        public void StopApplication()
        {
            _client?.Dispose();
            _factory?.Dispose();
            _baseFactory?.Dispose();
        }

        // ---------------------------------------------------------- Given

        [Given("I am on the create organisation page")]
        public async Task GivenIAmOnTheCreateOrganisationPage()
        {
            _response = await Client.GetAsync(CreatePath);
            _response.EnsureSuccessStatusCode();
            _page = await ParseAsync(_response);
        }

        [Given("an organisation with CVR {string} exists")]
        public async Task GivenAnOrganisationWithCvrExists(string cvr)
        {
            await using var db = CreateDbContext();
            var service = new OrganizationService(
                new OrganizationRepository(db),
                new ActivityLogRepository(db),
                new UnitOfWork(db));

            await service.CreateAsync("Acme ApS", cvr, Sector.Manufacturing, OrganizationSize.Small, "Anna");
        }

        // ---------------------------------------------------------- When

        [When("I submit the form with name {string}, CVR {string}, sector {string}, size {string} and my name {string}")]
        public async Task WhenISubmitTheForm(string name, string cvr, string sector, string size, string creatorName)
        {
            // Udfylder felterne og sender formularen med det skjulte antiforgery-token fra siden
            var token = _page!.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")!.Value;

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Cvr"] = cvr,
                ["Sector"] = sector,
                ["Size"] = size,
                ["CreatorName"] = creatorName,
                ["__RequestVerificationToken"] = token
            });

            // Følger redirect'en til detaljesiden automatisk
            _response = await Client.PostAsync(CreatePath, form);
            _page = await ParseAsync(_response);
        }

        [When("I post the form without an antiforgery token")]
        public async Task WhenIPostTheFormWithoutAnAntiforgeryToken()
        {
            // En ny klient uden cookies, som et forfalsket request fra et andet website
            using var attacker = Factory.CreateClient();

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = "Evil ApS",
                ["Cvr"] = "66666666",
                ["Sector"] = "Other",
                ["Size"] = "Small",
                ["CreatorName"] = "Mallory"
            });

            _response = await attacker.PostAsync(CreatePath, form);
        }

        // ---------------------------------------------------------- Then

        [Then("I see the details of the organisation {string}")]
        public void ThenISeeTheDetailsOfTheOrganisation(string name)
        {
            Assert.True(_response!.IsSuccessStatusCode);
            Assert.Equal(name, _page!.QuerySelector("#organisation-name")?.TextContent.Trim());
        }

        [Then("{string} is shown as Admin")]
        public void ThenIsShownAsAdmin(string userName)
        {
            var rows = _page!.QuerySelectorAll("#organisation-users tbody tr")
                .Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToArray());

            Assert.Contains(rows, cells => cells[0] == userName && cells[1] == "Admin");
        }

        [Then("I see the error {string}")]
        public void ThenISeeTheError(string message)
        {
            var errors = _page!.QuerySelectorAll(".validation-summary-errors li")
                .Select(item => item.TextContent.Trim());

            Assert.Contains(message, errors);
        }

        [Then("the name field still contains {string}")]
        public void ThenTheNameFieldStillContains(string name)
        {
            Assert.Equal(name, _page!.QuerySelector<IHtmlInputElement>("input[name='Name']")?.Value);
        }

        [Then("the request is rejected with status {int}")]
        public void ThenTheRequestIsRejectedWithStatus(int statusCode)
        {
            Assert.Equal(statusCode, (int)_response!.StatusCode);
        }

        [Then("no organisation is saved")]
        public async Task ThenNoOrganisationIsSaved()
        {
            await using var db = CreateDbContext();
            Assert.False(await db.Organizations.AnyAsync());
        }
    }
}
