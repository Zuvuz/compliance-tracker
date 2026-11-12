using ComplianceTracker.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComplianceTracker.UnitTests.Diagnostics;

// Unit tests of the error rate warning (US-098 R5). Every test follows Arrange / Act / Assert.
public class ErrorRateMonitorTests
{
    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static (ErrorRateMonitor Monitor, FakeTimeProvider Clock) Make()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
        return (new ErrorRateMonitor(clock, NullLogger<ErrorRateMonitor>.Instance), clock);
    }

    private static List<bool> Errors(ErrorRateMonitor monitor, int count) => Enumerable.Range(0, count).Select(_ => monitor.Record()).ToList();

    [Fact]
    public void Ten_errors_are_not_yet_a_high_rate()
        => Assert.DoesNotContain(true, Errors(Make().Monitor, 10));

    [Fact]
    public void The_eleventh_error_within_5_minutes_is_reported_once()
    {
        var results = Errors(Make().Monitor, 15);

        Assert.Equal(10, results.FindIndex(r => r));
        Assert.Single(results, r => r);
    }

    [Fact]
    public void Errors_spread_over_more_than_5_minutes_are_not_a_high_rate()
    {
        var (monitor, clock) = Make();

        var results = Enumerable.Range(0, 30).Select(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(31));
            return monitor.Record();
        }).ToList();

        Assert.DoesNotContain(true, results);
    }

    [Fact]
    public void A_new_burst_after_a_quiet_window_is_reported_again()
    {
        var (monitor, clock) = Make();
        Errors(monitor, 11);

        clock.Advance(TimeSpan.FromMinutes(6));
        var again = Errors(monitor, 11);

        Assert.Single(again, r => r);
    }

    [Fact]
    public void A_burst_within_the_same_window_is_not_reported_twice()
    {
        var (monitor, clock) = Make();
        Errors(monitor, 11);

        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.DoesNotContain(true, Errors(monitor, 20));
    }
}
