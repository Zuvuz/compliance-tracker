namespace ComplianceTracker.Application.Common
{
    // Gemmer alle ændringer fra repositories i én transaktion: enten lykkes det hele, eller intet gemmes.
    public interface IUnitOfWork
    {
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
