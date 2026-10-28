namespace ComplianceTracker.Specs.Support
{
    // The test database for the current scenario. Shared between the step classes in the same
    // scenario through Reqnroll's context injection (they ask for it in their constructor).
    public class DatabaseScenario
    {
        // Set by "Given an empty database"
        public string ConnectionString { get; set; } = "";
    }
}
