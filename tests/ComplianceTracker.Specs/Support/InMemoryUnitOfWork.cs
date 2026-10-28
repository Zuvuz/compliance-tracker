using ComplianceTracker.Application.Common;

namespace ComplianceTracker.Specs.Support
{
    // The in-memory repositories store things straight away, so there is nothing to save here
    public class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
