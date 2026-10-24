using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Fælles database-steps for alle integrationstests (US-002, US-003)
    [Binding]
    public class DatabaseStepDefinitions
    {
        private readonly DatabaseScenario _database;

        public DatabaseStepDefinitions(DatabaseScenario database)
        {
            _database = database;
        }

        [Given("an empty database")]
        public async Task GivenAnEmptyDatabase()
        {
            _database.ConnectionString = await TestDatabase.GetConnectionStringAsync();
            await TestDatabase.ResetAsync(_database.ConnectionString);
        }
    }
}
