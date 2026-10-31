namespace ComplianceTracker.Specs.Support
{
    // The clock for one scenario (US-016 R2). It tells the real time until a step says
    // "Given the time is ...", and from then on the fixed time, so a test can check the exact log time.
    // Shared by all step classes in the scenario through Reqnroll's context injection,
    // and given to the services (in-memory tests) and to the web app (integration tests).
    public sealed class ScenarioClock : TimeProvider
    {
        private DateTimeOffset? _fixedTime;

        public void SetUtcNow(DateTimeOffset time) => _fixedTime = time;

        public override DateTimeOffset GetUtcNow() => _fixedTime ?? base.GetUtcNow();
    }
}
