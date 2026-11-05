using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-104: the Product requirements tab (Views/Projects/_ProductRequirementsTab.cshtml): the form #requirement-methods
    // with checkboxes input[name=methods], the sections .method-section[data-method], and the tab links #project-tabs a[data-tab].
    [Binding]
    public class ProductRequirementsStepDefinitions
    {
        private const string Tab = "product-requirements";

        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;
        private string? _migrationDatabase;

        public ProductRequirementsStepDefinitions(WebScenario web, ScenarioClock clock)
        {
            _web = web;
            _clock = clock;
        }

        // R6: a database of its own, at the migration before this story, with a project linked to GitHub
        [Given("a project linked to GitHub was made before the methods existed")]
        public async Task GivenAProjectLinkedToGitHubBefore()
        {
            _migrationDatabase = $"methods_migration_{Guid.NewGuid():N}";
            await using (var admin = new NpgsqlConnection(MainConnectionString()))
            {
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_migrationDatabase}\"", admin);
                await create.ExecuteNonQueryAsync();
            }

            var connection = MigrationConnectionString();
            await using (var db = TestDatabase.CreateContext(connection))
            {
                await db.Database.MigrateAsync();
            }

            await using (var services = TestServices.Create(_clock, new TestCurrentUser(TestUsers.Identity("Anna")), TestServices.Database(connection)))
            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<OrganizationService>()
                    .CreateAsync("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small);
            }

            await using (var db = TestDatabase.CreateContext(connection))
            {
                var organization = await db.Organizations.SingleAsync();
                var project = Project.Create(organization.Id, "Acme Portal", null, "A portal", null, LifecyclePhase.Development);
                project.LinkGitHub("acme/portal", null, organization.Users.Single().Id, DateTime.UtcNow);
                db.Projects.Add(project);
                await db.SaveChangesAsync();

                var migrations = db.Database.GetMigrations().ToList();
                var before = migrations[migrations.FindIndex(m => m.EndsWith("_AddRequirementMethods", StringComparison.Ordinal)) - 1];
                await db.GetService<IMigrator>().MigrateAsync(before);
            }
        }

        [When("the database with that project is migrated")]
        public async Task WhenTheDatabaseIsMigrated()
        {
            await using var db = TestDatabase.CreateContext(MigrationConnectionString());
            await db.Database.MigrateAsync();
        }

        [Then("that project uses only {string}")]
        public async Task ThenThatProjectUsesOnly(string method)
        {
            await using var db = TestDatabase.CreateContext(MigrationConnectionString());
            var project = await db.Projects.SingleAsync();
            Assert.Equal(method, RequirementMethodNames.Describe(project.RequirementMethods));
        }

        private string MainConnectionString()
            => _web.App.Factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["ConnectionStrings:ComplianceTracker"]!;

        private string MigrationConnectionString()
            => new NpgsqlConnectionStringBuilder(MainConnectionString()) { Database = _migrationDatabase }.ConnectionString;

        // ---------------------------------------------------------- When

        [When("I open the old user stories address of {string}")]
        public async Task WhenIOpenTheOldAddress(string project)
            => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/user-stories");

        [Given("I have chosen the methods {string} for {string}")]
        [When("I choose the methods {string} for {string}")]
        public Task WhenIChooseTheMethod(string method, string project)
            => ChooseAsync(project, method);

        [When("I choose the methods {string} and {string} for {string}")]
        public Task WhenIChooseTheMethods(string first, string second, string project)
            => ChooseAsync(project, first, second);

        [When("I choose no methods for {string}")]
        public Task WhenIChooseNoMethods(string project)
            => ChooseAsync(project);

        // ---------------------------------------------------------- Then

        [Then("the project has the tab {string}")]
        public void ThenTheProjectHasTheTab(string title)
            => Assert.Contains(title, TabTitles());

        [Then("it has no tab {string}")]
        public void ThenItHasNoTab(string title)
            => Assert.DoesNotContain(title, TabTitles());

        [Then("I am on the {string} tab of {string}")]
        public async Task ThenIAmOnTheTab(string title, string project)
        {
            Assert.Equal($"/Projects/Details/{await ProjectIdAsync(project)}/{Tab}", _web.CurrentPath, ignoreCase: true);
            Assert.Equal(title, _web.Page!.QuerySelector("#project-tabs a.active")?.TextContent.Trim());
        }

        [Then("the tab shows the sections {string}, {string} and {string}")]
        public void ThenTheTabShowsTheSections(string first, string second, string third)
            => Assert.Equal(new[] { first, second, third }.Order(), Sections().Order());

        [Then("the tab shows the section {string}")]
        public void ThenTheTabShowsTheSection(string method)
            => Assert.Contains(method, Sections());

        [Then("the tab does not show the section {string}")]
        public void ThenTheTabDoesNotShowTheSection(string method)
            => Assert.DoesNotContain(method, Sections());

        [Then("the tab lists the work item {string}")]
        public void ThenTheTabListsTheWorkItem(string title)
            => Assert.Contains(title, _web.Page!.QuerySelectorAll(".work-item .work-item-title").Select(t => t.TextContent.Trim()));

        [Then("{string} cannot change the methods")]
        public void ThenCannotChangeTheMethods(string user)
        {
            Assert.Null(_web.Page!.QuerySelector("#requirement-methods"));
            Assert.NotNull(_web.Page!.QuerySelector("#requirement-methods-readonly"));
        }

        // ---------------------------------------------------------- Helpers

        // Ticks exactly the given methods, by the text of their labels, and sends the form
        private async Task ChooseAsync(string project, params string[] methods)
        {
            await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync(project)}/{Tab}");
            var form = _web.Page!.QuerySelector("#requirement-methods")
                ?? throw new Xunit.Sdk.XunitException("The tab has no form to choose the methods.");

            var fields = form.QuerySelectorAll("input[type=hidden]")
                .Select(i => new KeyValuePair<string, string>(i.GetAttribute("name")!, i.GetAttribute("value") ?? ""))
                .ToList();
            foreach (var method in methods)
            {
                var label = form.QuerySelectorAll("label").SingleOrDefault(l => l.TextContent.Trim() == method)
                    ?? throw new Xunit.Sdk.XunitException($"There is no method '{method}'.");
                var box = form.QuerySelector($"#{label.GetAttribute("for")}")!;
                fields.Add(new("methods", box.GetAttribute("value")!));
            }

            if (fields.All(f => f.Key != "__RequestVerificationToken"))
            {
                fields.Add(new("__RequestVerificationToken", _web.AntiforgeryToken()));
            }

            _web.Response = await _web.Client.PostAsync(form.GetAttribute("action")!, new FormUrlEncodedContent(fields));
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        private IEnumerable<string> TabTitles()
            => _web.Page!.QuerySelectorAll("#project-tabs a[data-tab]").Select(a => a.TextContent.Trim());

        private IReadOnlyList<string> Sections()
            => _web.Page!.QuerySelectorAll(".method-section").Select(s => s.GetAttribute("data-method")!).ToList();

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }
    }
}
