using ComplianceTracker.Application.Users;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-004 with test sign-in (TestAuthHandler) instead of Keycloak.
    // The real Keycloak sign-in is tested in KeycloakSignInStepDefinitions.
    [Binding]
    public class SignInStepDefinitions
    {
        private readonly WebScenario _web;

        public SignInStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("I am not signed in")]
        public void GivenIAmNotSignedIn()
        {
            // The client sends no X-Test-User header, so it is not signed in
        }

        [Given("I am signed in as {string} with e-mail {string} for the first time")]
        public void GivenIAmSignedInForTheFirstTime(string name, string email)
        {
            _web.SignInAs(name, email);
        }

        [Given("{string} has created the organisation {string}")]
        public async Task GivenHasCreatedTheOrganisation(string userName, string organisationName)
        {
            await _web.CreateOrganisationAsync(userName, organisationName);
        }

        // ---------------------------------------------------------- When

        [When("I open the front page")]
        [When("I open the application")]
        public async Task WhenIOpenTheFrontPage()
        {
            await _web.OpenAsync("/");
        }

        [When("I open the create organisation page")]
        public async Task WhenIOpenTheCreateOrganisationPage()
        {
            await _web.OpenAsync("/Organizations/Create");
        }

        [When("I create the organisation {string} with CVR {string}, sector {string} and size {string}")]
        public async Task WhenICreateTheOrganisation(string name, string cvr, string sector, string size)
        {
            await _web.OpenAsync("/Organizations/Create");
            await _web.PostFormAsync("/Organizations/Create", new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Cvr"] = cvr,
                ["Sector"] = sector,
                ["Size"] = size
            });
        }

        [When("I post the create form for {string} with CVR {string}")]
        public async Task WhenIPostTheCreateFormFor(string name, string cvr)
        {
            // The create page redirects to the user's organisation, but the menu's sign-out form
            // has a valid antiforgery token that a "manual" POST can use
            await _web.OpenAsync("/");
            await _web.PostFormAsync("/Organizations/Create", new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Cvr"] = cvr,
                ["Sector"] = "Health",
                ["Size"] = "Medium"
            });
        }

        [When("I open the page of {string}")]
        public async Task WhenIOpenThePageOf(string organisationName)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Organizations.Where(o => o.Name == organisationName).Select(o => o.Id).SingleAsync();

            await _web.OpenAsync($"/Organizations/Details/{id}");
        }

        [When("{string} signs in")]
        public async Task WhenSignsIn(string userName)
        {
            // The same code the app runs when Keycloak has confirmed a sign-in (OnTokenValidated in Program.cs)
            using var scope = _web.App.Factory.Services.CreateScope();
            var activity = scope.ServiceProvider.GetRequiredService<UserActivityService>();
            await activity.RecordSignInAsync(TestUsers.Identity(userName).ExternalId);
        }

        [When("{string} signs out")]
        public async Task WhenSignsOut(string userName)
        {
            // Sign out through the menu's form, like a user would
            _web.SignInAs(userName);
            await _web.OpenAsync("/");
            await _web.PostFormAsync("/Account/Logout", new Dictionary<string, string>());

            // The app redirects to Keycloak's sign-out page, so the user is signed out there too
            Assert.EndsWith("/protocol/openid-connect/logout", _web.CurrentPath);
        }

        // ---------------------------------------------------------- Then

        [Then("I see the front page")]
        public void ThenISeeTheFrontPage()
        {
            Assert.Equal(HttpStatusCode.OK, _web.Response!.StatusCode);
            Assert.NotNull(_web.Page!.QuerySelector("#front-page"));
        }

        [Then("I am sent to sign in")]
        public void ThenIAmSentToSignIn()
        {
            Assert.Equal(TestAuthHandler.SignInPath, _web.CurrentPath);
        }

        [Then("I am sent to create my organisation")]
        public void ThenIAmSentToCreateMyOrganisation()
        {
            Assert.Equal("/Organizations/Create", _web.CurrentPath);
            Assert.NotNull(_web.Page!.QuerySelector("input[name='Name']"));
            Assert.Null(_web.Page!.QuerySelector("input[name='CreatorName']"));
        }

        [Then("the page is not found")]
        public void ThenThePageIsNotFound()
        {
            Assert.Equal(HttpStatusCode.NotFound, _web.Response!.StatusCode);
        }

        [Then("the activity log for {string} has {string} and {string} for {string}")]
        public async Task ThenTheActivityLogHas(string organisationName, string firstAction, string secondAction, string userName)
        {
            await using var db = _web.CreateDbContext();
            var organisation = await db.Organizations.SingleAsync(o => o.Name == organisationName);
            var user = organisation.Users.Single(u => u.Name == userName);

            var actions = await db.ActivityLog
                .Where(e => e.OrganizationId == organisation.Id && e.UserId == user.Id)
                .Select(e => e.Action)
                .ToListAsync();

            Assert.Contains(Enum.Parse<ActivityAction>(firstAction), actions);
            Assert.Contains(Enum.Parse<ActivityAction>(secondAction), actions);
        }
    }
}
