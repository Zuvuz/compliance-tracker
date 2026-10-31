using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace ComplianceTracker.Specs.Support
{
    // US-016 R3: makes the database "fail" when the activity log is written, as if the server
    // went down halfway through saving. Any command that inserts into ActivityLog is stopped with an error,
    // so the test can prove the organisation saved in the same transaction is rolled back too.
    public sealed class FailingActivityLogInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfActivityLogInsert(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfActivityLogInsert(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void ThrowIfActivityLogInsert(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO \"ActivityLog\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Simulated failure while saving the activity log");
            }
        }
    }
}
