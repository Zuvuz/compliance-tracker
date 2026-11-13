using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ComplianceTracker.Specs.Support
{
    // US-098: the app's log lines in the scenarios, with the values of their scopes (e.g. the reference and the user)
    public sealed class CapturedLogs : ILoggerProvider, ISupportExternalScope
    {
        public record Entry(string Category, LogLevel Level, int EventId, string Message, IReadOnlyDictionary<string, object?> Scope);

        private readonly ConcurrentQueue<Entry> _entries = new();
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public IReadOnlyList<Entry> Entries => _entries.ToList();

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class Logger(CapturedLogs owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var scope = new Dictionary<string, object?>();
                owner._scopes.ForEachScope((value, into) =>
                {
                    if (value is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        foreach (var (key, item) in pairs)
                        {
                            into[key] = item;
                        }
                    }
                }, scope);
                owner._entries.Enqueue(new Entry(category, logLevel, eventId.Id, formatter(state, exception), scope));
            }
        }
    }
}
