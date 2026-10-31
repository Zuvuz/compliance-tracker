using ComplianceTracker.Application;
using ComplianceTracker.Specs.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-029: checks of how the app is put together (dependency injection, configuration and layers).
    [Binding]
    public sealed class ApplicationStructureStepDefinitions : IDisposable
    {
        // Only start-up is tested here, so no database is needed; the app never connects to this address
        private const string UnusedDatabase = "Host=unused;Database=unused";

        private readonly WebScenario _web;
        private readonly ScenarioClock _clock;
        private readonly Dictionary<string, string?> _settings = new();
        private TestApp? _app;
        private Exception? _startError;

        public ApplicationStructureStepDefinitions(WebScenario web, ScenarioClock clock)
        {
            _web = web;
            _clock = clock;
        }

        public void Dispose() => _app?.Dispose();

        // ---------------------------------------------------------- Given

        // An empty value counts as missing, like an unset environment variable
        [Given("the Keycloak setting {string} is missing")]
        public void GivenTheKeycloakSettingIsMissing(string name)
            => _settings[$"Authentication:Keycloak:{name}"] = "";

        // ---------------------------------------------------------- When

        // Building the app runs the start-up checks: DI validation (R5) and the Keycloak options (R3)
        [When("the application starts")]
        public void WhenTheApplicationStarts()
        {
            _app = TestApp.WithTestSignIn(UnusedDatabase, _clock, _settings);
            try
            {
                _ = _app.Factory.Services;
            }
            catch (Exception ex)
            {
                _startError = ex;
            }
        }

        // ---------------------------------------------------------- Then

        [Then("it stops with the message {string}")]
        public void ThenItStopsWithTheMessage(string message)
        {
            Assert.NotNull(_startError);
            Assert.Contains(message, AllMessages(_startError));
        }

        // R5: every controller and every Application service can be built from the container
        [Then("the container can create every controller and service")]
        public void ThenTheContainerCanCreateEveryControllerAndService()
        {
            Assert.Null(_startError);
            using var scope = _app!.Factory.Services.CreateScope();

            foreach (var type in ControllerTypes().Concat(ApplicationServiceTypes()))
            {
                Assert.NotNull(scope.ServiceProvider.GetRequiredService(type));
            }
        }

        // R7: controllers depend on use cases and queries only, never on storage
        [Then("no controller depends on a repository")]
        public void ThenNoControllerDependsOnARepository()
        {
            var offenders =
                from controller in ControllerTypes()
                from constructor in controller.GetConstructors()
                from parameter in constructor.GetParameters()
                let type = parameter.ParameterType
                where type.Name.EndsWith("Repository") || type.Name == "IUnitOfWork" || type == typeof(DbContext) || type.IsSubclassOf(typeof(DbContext))
                select $"{controller.Name} → {type.Name}";

            Assert.Empty(offenders);
        }

        // R1: the project lands in the signed-in user's organisation, not in another one
        [Then("the project {string} belongs to {string}")]
        public async Task ThenTheProjectBelongsTo(string projectName, string organisationName)
        {
            await using var db = _web.CreateDbContext();
            var organisationId = await db.Organizations.Where(o => o.Name == organisationName).Select(o => o.Id).SingleAsync();
            var project = await db.Projects.SingleAsync(p => p.Name == projectName);

            Assert.Equal(organisationId, project.OrganizationId);
        }

        // ---------------------------------------------------------- Helpers

        private static IEnumerable<Type> ControllerTypes()
            => typeof(Program).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Controller)));

        // The use cases and queries in the Application layer (classes named ...Service or ...Queries)
        private static IEnumerable<Type> ApplicationServiceTypes()
            => typeof(DependencyInjection).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && (t.Name.EndsWith("Service") || t.Name.EndsWith("Queries")));

        private static string AllMessages(Exception error)
        {
            var messages = new List<string>();
            for (Exception? e = error; e is not null; e = e.InnerException)
            {
                messages.Add(e.Message);
                if (e is AggregateException aggregate)
                {
                    messages.AddRange(aggregate.InnerExceptions.Select(AllMessages));
                }
            }

            return string.Join(" | ", messages);
        }
    }
}
