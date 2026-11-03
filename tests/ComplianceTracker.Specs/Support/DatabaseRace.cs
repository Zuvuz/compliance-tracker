using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace ComplianceTracker.Specs.Support
{
    // US-015: lets a scenario do something "at the same moment" as the web app saves, e.g. another person saving
    // the same CVR. The action runs once, just before the app's INSERT into the table, after the app's own checks.
    // Reqnroll creates one per scenario; WebScenario adds its interceptor to the web app's DbContext.
    public sealed class DatabaseRace
    {
        private Func<Task>? _beforeInsert;
        private string? _table;

        public IInterceptor Interceptor => new RaceInterceptor(this);

        public void BeforeInsertInto(string table, Func<Task> action)
        {
            _table = table;
            _beforeInsert = action;
        }

        // Taken once, so the action's own saves are not stopped again
        private Func<Task>? Take(string commandText)
        {
            if (_beforeInsert is null || !commandText.Contains($"INSERT INTO \"{_table}\"", StringComparison.Ordinal))
            {
                return null;
            }

            var action = _beforeInsert;
            _beforeInsert = null;
            return action;
        }

        private sealed class RaceInterceptor(DatabaseRace race) : DbCommandInterceptor
        {
            public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                if (race.Take(command.CommandText) is { } action)
                {
                    await action();
                }

                return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }

            public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (race.Take(command.CommandText) is { } action)
                {
                    await action();
                }

                return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
            }
        }
    }
}
