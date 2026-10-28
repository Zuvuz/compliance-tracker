using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // Shared database step for all integration tests (US-002 and later)
    [Binding]
    public class DatabaseStepDefinitions
    {
        private readonly DatabaseScenario _database;

        public DatabaseStepDefinitions(DatabaseScenario database)
        {
            _database = database;
        }

        // Starts the test database (first time only) and empties it, so every scenario starts clean
        [Given("an empty database")]
        public async Task GivenAnEmptyDatabase()
        {
            _database.ConnectionString = await TestDatabase.GetConnectionStringAsync();
            await TestDatabase.ResetAsync(_database.ConnectionString);
        }
    }
}
