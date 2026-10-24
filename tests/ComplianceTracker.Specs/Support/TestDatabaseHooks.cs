namespace ComplianceTracker.Specs.Support
{
    [Binding]
    public class TestDatabaseHooks
    {
        [AfterTestRun]
        public static Task StopDatabase() => TestDatabase.DisposeAsync();
    }
}
