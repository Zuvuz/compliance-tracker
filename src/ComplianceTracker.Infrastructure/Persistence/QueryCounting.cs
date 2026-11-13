using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // US-139: the number of database queries of one request (one per scope, like the DbContext)
    public sealed class RequestQueryCounter
    {
        public int Count { get; private set; }

        public void Add() => Count++;
    }

    // US-139: told how many queries a request made; does nothing in the app, the tests check the limits with it
    public interface IQueryCountObserver
    {
        void Counted(string path, int queries);
    }

    // The page a query was made for (the web app knows it); null outside a request
    public interface ICurrentRequestPath
    {
        string? Path { get; }
    }

    public sealed class NoRequestPath : ICurrentRequestPath
    {
        public string? Path => null;
    }

    public sealed class NoQueryCountObserver : IQueryCountObserver
    {
        public void Counted(string path, int queries)
        {
        }
    }

    // US-139 R5, R6: counts every query EF Core runs and logs one that is slower than the limit (500 ms by default,
    // "Database:SlowQueryMilliseconds"), with the page but never with its parameter values. EF Core's own interceptor.
    public sealed partial class QueryCountingInterceptor : DbCommandInterceptor
    {
        public const string SlowQuerySetting = "Database:SlowQueryMilliseconds";
        public const int DefaultSlowQueryMilliseconds = 500;

        private readonly RequestQueryCounter _counter;
        private readonly ICurrentRequestPath _path;
        private readonly ILogger<QueryCountingInterceptor> _log;
        private readonly TimeSpan _slow;

        public QueryCountingInterceptor(RequestQueryCounter counter, ICurrentRequestPath path, ILogger<QueryCountingInterceptor> log, IConfiguration configuration)
        {
            _counter = counter;
            _path = path;
            _log = log;
            _slow = TimeSpan.FromMilliseconds(configuration.GetValue(SlowQuerySetting, DefaultSlowQueryMilliseconds));
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Ran(eventData);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Ran(eventData);
            return ValueTask.FromResult(result);
        }

        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            Ran(eventData);
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Ran(eventData);
            return ValueTask.FromResult(result);
        }

        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        {
            Ran(eventData);
            return result;
        }

        public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        {
            Ran(eventData);
            return ValueTask.FromResult(result);
        }

        private void Ran(CommandExecutedEventData eventData) => Record(eventData.Duration);

        // One query has run: counted, and logged when it was slow
        public void Record(TimeSpan duration)
        {
            _counter.Add();
            if (duration > _slow)
            {
                SlowQuery(_log, (long)duration.TotalMilliseconds, _path.Path ?? "(no request)");
            }
        }

        // The page, never the SQL's parameter values (R5)
        [LoggerMessage(Level = LogLevel.Warning, Message = "Slow database query: {Milliseconds} ms on {Path}")]
        private static partial void SlowQuery(ILogger logger, long milliseconds, string path);
    }
}
