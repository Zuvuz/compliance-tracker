using ComplianceTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ComplianceTracker.UnitTests.Persistence;

// Unit tests of the query counter and the slow-query log (US-139). Every test follows Arrange / Act / Assert.
public class QueryCountingTests
{
    private sealed class TestLog : ILogger<QueryCountingInterceptor>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FixedPath(string? path) : ICurrentRequestPath
    {
        public string? Path { get; } = path;
    }

    private static (QueryCountingInterceptor Interceptor, RequestQueryCounter Counter, TestLog Log) Make(int? slowMilliseconds = null, string? path = "/Projects")
    {
        var settings = new Dictionary<string, string?>();
        if (slowMilliseconds is { } ms)
        {
            settings[QueryCountingInterceptor.SlowQuerySetting] = ms.ToString();
        }

        var counter = new RequestQueryCounter();
        var log = new TestLog();
        var interceptor = new QueryCountingInterceptor(counter, new FixedPath(path), log, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return (interceptor, counter, log);
    }

    [Fact]
    public void Every_query_is_counted()
    {
        var (interceptor, counter, _) = Make();

        interceptor.Record(TimeSpan.FromMilliseconds(3));
        interceptor.Record(TimeSpan.FromMilliseconds(4));

        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public void A_query_over_500_ms_is_logged_with_the_page_as_a_warning()
    {
        var (interceptor, _, log) = Make();

        interceptor.Record(TimeSpan.FromMilliseconds(501));

        Assert.Equal((LogLevel.Warning, "Slow database query: 501 ms on /Projects"), log.Entries.Single());
    }

    [Fact]
    public void A_query_of_exactly_the_limit_is_not_slow()
    {
        var (interceptor, _, log) = Make();

        interceptor.Record(TimeSpan.FromMilliseconds(500));

        Assert.Empty(log.Entries);
    }

    [Fact]
    public void The_limit_is_a_setting()
    {
        var (interceptor, _, log) = Make(slowMilliseconds: 50);

        interceptor.Record(TimeSpan.FromMilliseconds(60));

        Assert.Single(log.Entries);
    }

    [Fact]
    public void Outside_a_request_the_log_says_so()
    {
        var (interceptor, _, log) = Make(path: null);

        interceptor.Record(TimeSpan.FromSeconds(2));

        Assert.EndsWith("on (no request)", log.Entries.Single().Text);
    }

    [Fact]
    public void The_observer_in_the_app_does_nothing()
        => new NoQueryCountObserver().Counted("/", 10); // no exception, nothing kept
}
