using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Integrationstests mod en rigtig PostgreSQL-database.
    // "Applikationen genstarter" = en ny DbContext uden noget i hukommelsen, så alt skal hentes fra databasen.
    [Binding]
    public class PersistOrganisationsStepDefinitions
    {
        private readonly DatabaseScenario _database;
        private ComplianceTrackerDbContext? _db;
        private OrganizationService? _service;

        private Organization? _created;
        private Organization? _retrieved;

        public PersistOrganisationsStepDefinitions(DatabaseScenario database)
        {
            _database = database;
        }

        // ---------------------------------------------------------- Hjælpere

        private OrganizationService Service
        {
            get
            {
                if (_service is null)
                {
                    StartApplication();
                }

                return _service!;
            }
        }

        private void StartApplication()
        {
            _db?.Dispose();
            _db = TestDatabase.CreateContext(_database.ConnectionString);
            _service = new OrganizationService(
                new OrganizationRepository(_db),
                new ActivityLogRepository(_db),
                new UnitOfWork(_db));
        }

        private Task<Organization?> LoadCreatedOrganization()
            => new OrganizationRepository(_db!).GetByIdAsync(_created!.Id);

        [AfterScenario]
        public void StopApplication() => _db?.Dispose();

        // ---------------------------------------------------------- Given

        [Given("I have created the organisation {string}")]
        public async Task GivenIHaveCreatedTheOrganisation(string name)
        {
            _created = await Service.CreateAsync(name, "12345678", Sector.Manufacturing, OrganizationSize.Small, TestUsers.Identity("Anna"));
        }

        [Given("I have created an organisation with name {string}")]
        public async Task GivenIHaveCreatedAnOrganisationWithName(string name)
        {
            // Andre værdier end standard-enum-værdierne, så en forkert mapping ikke kan snyde testen
            _created = await Service.CreateAsync(name, "87654321", Sector.Health, OrganizationSize.Medium, TestUsers.Identity("Bo"));
        }

        // ---------------------------------------------------------- When

        [When("the application restarts")]
        public void WhenTheApplicationRestarts()
        {
            StartApplication();
        }

        [When("I retrieve the organisation")]
        public async Task WhenIRetrieveTheOrganisation()
        {
            // Ny DbContext, så organisationen hentes fra databasen og ikke fra EF's hukommelse
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

        [Then("all its data matches what was created")]
        public void ThenAllItsDataMatchesWhatWasCreated()
        {
            Assert.NotNull(_created);
            Assert.NotNull(_retrieved);
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
