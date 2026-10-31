using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-002: integration tests against a real PostgreSQL database.
    // "The application restarts" = a new DbContext with nothing in memory, so everything must come from the database.
    [Binding]
    public class PersistOrganisationsStepDefinitions
    {
        private readonly DatabaseScenario _database;
        private readonly ScenarioClock _clock;
        private ComplianceTrackerDbContext? _db;
        private ServiceProvider? _services;
        private readonly TestCurrentUser _signedIn = new(TestUsers.Identity("Anna"));

        private Organization? _created;
        private Organization? _retrieved;

        // US-016 R3: set when the database should fail while writing the activity log, and the error it gave
        private bool _activityLogFails;
        private Exception? _failure;

        public PersistOrganisationsStepDefinitions(DatabaseScenario database, ScenarioClock clock)
        {
            _database = database;
            _clock = clock;
        }

        // ---------------------------------------------------------- Helpers

        // The "running application": started the first time a step needs it
        private ServiceProvider Services
        {
            get
            {
                if (_services is null)
                {
                    StartApplication();
                }

                return _services!;
            }
        }

        // Throws away the old application and DbContext (and everything they remember) and builds them again.
        // The use cases come from AddApplication(), like in the web app (US-029).
        private void StartApplication()
        {
            _services?.Dispose();
            _db?.Dispose();
            _db = TestDatabase.CreateContext(_database.ConnectionString);
            var storage = _activityLogFails
                ? TestServices.Database(_database.ConnectionString, new FailingActivityLogInterceptor())
                : TestServices.Database(_database.ConnectionString);
            _services = TestServices.Create(_clock, _signedIn, storage);
        }

        // Runs the create use case as one "request", signed in as the user
        private async Task<Organization> CreateAsync(string userName, string name, string cvr, Sector sector, OrganizationSize size)
        {
            _signedIn.Identity = TestUsers.Identity(userName);
            await using var scope = Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<OrganizationService>().CreateAsync(name, cvr, sector, size);
        }

        private Task<Organization?> LoadCreatedOrganization()
            => new OrganizationRepository(_db!).GetByIdAsync(_created!.Id);

        [AfterScenario]
        public void StopApplication()
        {
            _services?.Dispose();
            _db?.Dispose();
        }

        // ---------------------------------------------------------- Given

        [Given("I have created the organisation {string}")]
        public async Task GivenIHaveCreatedTheOrganisation(string name)
        {
            _created = await CreateAsync("Anna", name, "12345678", Sector.Manufacturing, OrganizationSize.Small);
        }

        [Given("I have created an organisation with name {string}")]
        public async Task GivenIHaveCreatedAnOrganisationWithName(string name)
        {
            // Other values than the first enum values, so a wrong mapping cannot fool the test
            _created = await CreateAsync("Bo", name, "87654321", Sector.Health, OrganizationSize.Medium);
        }

        [Given("saving the activity log will fail")]
        public void GivenSavingTheActivityLogWillFail()
        {
            _activityLogFails = true;
            StartApplication();
        }

        // ---------------------------------------------------------- When

        // Like creating, but a failure is kept for the "Then" steps instead of failing the test
        [When("I try to create the organisation {string}")]
        public async Task WhenITryToCreateTheOrganisation(string name)
        {
            try
            {
                await CreateAsync("Anna", name, "12345678", Sector.Manufacturing, OrganizationSize.Small);
            }
            catch (Exception ex)
            {
                _failure = ex;
            }
        }

        [When("the application restarts")]
        public void WhenTheApplicationRestarts()
        {
            StartApplication();
        }

        [When("I retrieve the organisation")]
        public async Task WhenIRetrieveTheOrganisation()
        {
            // New DbContext, so the organisation is read from the database and not from EF's memory
            StartApplication();
            _retrieved = await LoadCreatedOrganization();
        }

        // ---------------------------------------------------------- Then

        [Then("the organisation {string} exists")]
        public async Task ThenTheOrganisationExists(string name)
        {
            var organization = await LoadCreatedOrganization();

            Assert.NotNull(organization);
            Assert.Equal(name, organization.Name);
        }

        [Then("the creation fails")]
        public void ThenTheCreationFails()
        {
            Assert.NotNull(_failure);
        }

        // R3: neither the organisation nor its log entry was stored (read with a fresh DbContext)
        [Then("the organisation {string} does not exist")]
        public async Task ThenTheOrganisationDoesNotExist(string name)
        {
            await using var db = TestDatabase.CreateContext(_database.ConnectionString);
            Assert.False(await db.Organizations.AnyAsync(o => o.Name == name));
            Assert.False(await db.ActivityLog.AnyAsync());
        }

        [Then("all its data matches what was created")]
        public void ThenAllItsDataMatchesWhatWasCreated()
        {
            Assert.NotNull(_created);
            Assert.NotNull(_retrieved);

            // Two different objects: proves the data really came from the database
            Assert.NotSame(_created, _retrieved);

            Assert.Equal(_created.Id, _retrieved.Id);
            Assert.Equal(_created.Name, _retrieved.Name);
            Assert.Equal(_created.Cvr, _retrieved.Cvr);
            Assert.Equal(_created.Sector, _retrieved.Sector);
            Assert.Equal(_created.Size, _retrieved.Size);

            var createdUser = Assert.Single(_created.Users);
            var retrievedUser = Assert.Single(_retrieved.Users);
            Assert.Equal(createdUser.Id, retrievedUser.Id);
            Assert.Equal(createdUser.Name, retrievedUser.Name);
            Assert.Equal(createdUser.Role, retrievedUser.Role);
        }

        [Then("an activity log entry exists for the creation of {string}")]
        public async Task ThenAnActivityLogEntryExistsForTheCreationOf(string name)
        {
            Assert.NotNull(_created);
            Assert.Equal(name, _created.Name);

            var entries = await _db!.ActivityLog
                .Where(e => e.OrganizationId == _created.Id)
                .ToListAsync();

            var entry = Assert.Single(entries);
            Assert.Equal(ActivityAction.OrganizationCreated, entry.Action);
            Assert.Equal(_created.Users.Single().Id, entry.UserId);
        }
    }
}
