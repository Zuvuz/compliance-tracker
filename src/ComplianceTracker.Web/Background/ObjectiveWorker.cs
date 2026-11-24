using ComplianceTracker.Application.Isms;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Web.Background
{
    // US-209 R2: every hour the tracker measures the objectives that are due. Reminders:RunInBackground switches it off,
    // like the reminders (the tests run it themselves).
    public class ObjectiveWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IOptions<ReminderWorkerOptions> _options;
        private readonly ILogger<ObjectiveWorker> _logger;

        public ObjectiveWorker(IServiceScopeFactory scopes, IOptions<ReminderWorkerOptions> options, ILogger<ObjectiveWorker> logger)
        {
            _scopes = scopes;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Value.RunInBackground)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var taken = await scope.ServiceProvider.GetRequiredService<ObjectiveService>().MeasureDueAsync(stoppingToken);
                    if (taken > 0)
                    {
                        _logger.LogInformation("Objectives: {Taken} measurements taken", taken);
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    _logger.LogError(error, "The objectives could not be measured");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
