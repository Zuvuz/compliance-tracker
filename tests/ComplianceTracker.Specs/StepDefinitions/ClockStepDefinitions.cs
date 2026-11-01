using ComplianceTracker.Specs.Support;
using System.Globalization;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-016 R2: fixes the time, so a scenario can check exactly when a log entry says something happened.
    // Works for both the in-memory and the web scenarios, because they share the same ScenarioClock.
    [Binding]
    public class ClockStepDefinitions
    {
        private readonly ScenarioClock _clock;

        public ClockStepDefinitions(ScenarioClock clock)
        {
            _clock = clock;
        }

        // The time is written as ISO 8601 in UTC, e.g. "2026-10-24T14:02:00Z"
        [Given("the time is {string}")]
        [When("the time is {string}")]
        public void GivenTheTimeIs(string time)
            => _clock.SetUtcNow(Parse(time));

        public static DateTimeOffset Parse(string time)
            => DateTimeOffset.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
