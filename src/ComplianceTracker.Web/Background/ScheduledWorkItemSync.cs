using ComplianceTracker.Application.WorkItems;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Background
{
    // US-018 R6: checks every linked GitHub repository at a fixed interval, so closed and changed issues
    // are updated without anyone pressing "Sync now". Runs inside the web app.
    public class ScheduledWorkItemSync : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IOptions<WorkItemSyncOptions> _options;
        private readonly ILogger<ScheduledWorkItemSync> _logger;

        public ScheduledWorkItemSync(IServiceScopeFactory scopes, IOptions<WorkItemSyncOptions> options, ILogger<ScheduledWorkItemSync> logger)
        {
            _scopes = scopes;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Value.Enabled)
            {
                return;
            }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.Value.IntervalMinutes));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // A new scope per run, like a request: its own DbContext and unit of work
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<WorkItemSyncService>().SyncAllAsync(stoppingToken);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // One failed run must not stop the next one
                    _logger.LogError(error, "The scheduled GitHub check failed");
                }
            }
        }
    }

    // Settings in the section "WorkItemSync". GitHub allows 60 requests per hour without a key,
    // so 15 minutes leaves room for a few repositories and "Sync now".
    public class WorkItemSyncOptions
    {
        public const string Section = "WorkItemSync";

        public bool Enabled { get; set; } = true;

        [Range(1, 1440, ErrorMessage = "WorkItemSync:IntervalMinutes must be between 1 and 1440")]
        public int IntervalMinutes { get; set; } = 15;
    }
}
