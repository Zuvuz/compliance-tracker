namespace ComplianceTracker.Specs.Support
{
    // Deles mellem step-klasserne i samme scenarie via Reqnroll's context injection.
    public class DatabaseScenario
    {
        public string ConnectionString { get; set; } = "";
    }
}
