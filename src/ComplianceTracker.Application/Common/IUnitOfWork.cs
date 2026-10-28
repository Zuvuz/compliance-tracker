namespace ComplianceTracker.Application.Common
{
    // Saves all changes registered by the repositories in one transaction:
    // either everything is saved, or nothing is (e.g. an organisation and its log entry).
    public interface IUnitOfWork
    {
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
