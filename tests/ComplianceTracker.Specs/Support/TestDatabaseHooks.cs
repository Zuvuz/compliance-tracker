namespace ComplianceTracker.Specs.Support
{
    [Binding]
    public class TestDatabaseHooks
    {
        // Stopper de containere, testkørslen har startet
        [AfterTestRun]
        public static async Task StopContainers()
        {
            await TestDatabase.DisposeAsync();
            await TestKeycloak.DisposeAsync();
        }
    }
}
