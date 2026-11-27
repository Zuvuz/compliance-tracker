using ComplianceTracker.Application.ActivityLog;

namespace ComplianceTracker.Web.Background
{
    // US-219 R6: once a day the tracker deletes the visits older than each organisation's retention period; the count goes
    // in the change log. A failed run is logged and tried again the next day.
    public class AccessLogCleanupWorker : BackgroundService
    {
        private static readonly TimeSpan Every = TimeSpan.FromDays(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AccessLogCleanupWorker> _logger;

        public AccessLogCleanupWorker(IServiceScopeFactory scopes, ILogger<AccessLogCleanupWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Not at start-up, so the tests and a fresh start are not slowed down
            using var timer = new PeriodicTimer(Every);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var deleted = await scope.ServiceProvider.GetRequiredService<AccessLogService>().CleanUpAsync(stoppingToken);
                    _logger.LogInformation("The access log clean-up deleted {Count} visits", deleted);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "The access log clean-up failed");
                }
            }
        }
    }
}
