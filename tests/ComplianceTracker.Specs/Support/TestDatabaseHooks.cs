namespace ComplianceTracker.Specs.Support
{
    // Reqnroll hooks that run around the whole test run
    [Binding]
    public class TestDatabaseHooks
    {
        // Stops the containers the test run started (Testcontainers would also clean them up by itself)
        [AfterTestRun]
        public static async Task StopContainers()
        {
            await TestDatabase.DisposeAsync();
            await TestKeycloak.DisposeAsync();
        }
    }
}
