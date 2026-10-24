using ComplianceTracker.Application.Common;

namespace ComplianceTracker.Specs.Support
{
    // In-memory-repositories gemmer med det samme, så der er intet at gemme her.
    public class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
