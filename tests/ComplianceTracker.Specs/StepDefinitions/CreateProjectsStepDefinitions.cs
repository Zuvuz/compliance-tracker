using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-008: creating projects, tested through the web app with test sign-in.
    // The steps define the UI contract: the form on /Projects/Create, the project page with
    // h1#project-name, and links with the class project-link in #organisation-projects.
    [Binding]
    public class CreateProjectsStepDefinitions
    {
        private const string CreatePath = "/Projects/Create";

        private readonly WebScenario _web;

        // The address of each project's page, so another user can try to open it
        private readonly Dictionary<string, string> _projectLinks = new();

        public CreateProjectsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given

        [Given("the project {string} exists")]
        public async Task GivenTheProjectExists(string name)
        {
            await CreateProject(name, "Test purpose");
            _projectLinks[name] = await ProjectLinkAsync(name);
        }

        // US-011 Background
        [Given("the projects {string} and {string} exist")]
        public async Task GivenTheProjectsExist(string first, string second)
        {
            await GivenTheProjectExists(first);
            await GivenTheProjectExists(second);
        }

        // ---------------------------------------------------------- When

        [When("I create the project {string} with intended purpose {string}")]
        public Task WhenICreateTheProject(string name, string intendedPurpose)
            => CreateProject(name, intendedPurpose);

        [When("I create the project {string} with no intended purpose")]
        public Task WhenICreateTheProjectWithNoIntendedPurpose(string name)
            => CreateProject(name, "");

        // US-016 R7: every field of the form
        [When("I create the project {string} with description {string}, intended purpose {string}, scope {string} and phase {string}")]
        public async Task WhenICreateTheProjectWithAllFields(string name, string description, string intendedPurpose, string scope, string phase)
        {
            await _web.OpenAsync(CreatePath);
            await _web.PostFormAsync(CreatePath, new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Description"] = description,
                ["IntendedPurpose"] = intendedPurpose,
                ["Scope"] = scope,
                ["LifecyclePhase"] = phase
            });
        }

        // Another user opens the project's address in their own "browser"
        [When("{string} opens the page of {string}")]
        public async Task WhenOpensThePageOf(string userName, string projectName)
        {
            using var other = _web.CreateClient(signedInAs: userName);
            _web.Response = await other.GetAsync(_projectLinks[projectName]);
        }

        // ---------------------------------------------------------- Then

        [Then("the organisation page lists the project {string}")]
        public async Task ThenTheOrganisationPageListsTheProject(string name)
        {
            // Finding the link proves the project is listed (R4)
            await ProjectLinkAsync(name);
        }

        // US-016 R7: read back from the project's own page
        [Then("the page of {string} shows description {string}, scope {string} and phase {string}")]
        public async Task ThenThePageShows(string projectName, string description, string scope, string phase)
        {
            await _web.OpenAsync(await ProjectLinkAsync(projectName));

            Assert.Equal(description, _web.Page!.QuerySelector("#project-description")?.TextContent.Trim());
            Assert.Equal(scope, _web.Page!.QuerySelector("#project-scope")?.TextContent.Trim());
            Assert.Equal(phase, _web.Page!.QuerySelector("#project-phase")?.TextContent.Trim());
        }

        // R6 (logging) uses the shared step in ActivityLogStepDefinitions

        // ---------------------------------------------------------- Helpers

        // Fills in the create form and submits it, like a user would
        private async Task CreateProject(string name, string intendedPurpose)
        {
            await _web.OpenAsync(CreatePath);
            await _web.PostFormAsync(CreatePath, new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Description"] = "",
                ["IntendedPurpose"] = intendedPurpose,
                ["Scope"] = "",
                ["LifecyclePhase"] = "Planning"
            });
        }

        // Opens the organisation's Overview and returns the address of the project's link
        private async Task<string> ProjectLinkAsync(string projectName)
        {
            await _web.OpenAsync("/Organizations/Mine");

            var link = _web.Page!.QuerySelectorAll("#organisation-projects a.project-link")
                .FirstOrDefault(a => a.TextContent.Trim() == projectName)
                ?? throw new Xunit.Sdk.XunitException($"The organisation page does not list the project '{projectName}'.");

            return link.GetAttribute("href")!;
        }
    }
}