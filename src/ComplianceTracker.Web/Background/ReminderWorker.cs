using ComplianceTracker.Application.Reminders;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Web.Background
{
    // US-183: sends the reminders every hour, so a 24-hour reporting deadline is not missed. Reminders:RunInBackground
    // switches it off (the tests run it themselves).
    public sealed class ReminderWorkerOptions
    {
        public const string Section = "Reminders";

        public bool RunInBackground { get; set; } = true;
        public string? BaseUrl { get; set; }
    }

    public class ReminderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IOptions<ReminderWorkerOptions> _options;
        private readonly ILogger<ReminderWorker> _logger;

        public ReminderWorker(IServiceScopeFactory scopes, IOptions<ReminderWorkerOptions> options, ILogger<ReminderWorker> logger)
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
                    var run = await scope.ServiceProvider.GetRequiredService<ReminderService>().RunAsync(stoppingToken);

                    // US-184 R3: organisations whose 30 days of closing are over
                    using var closing = _scopes.CreateScope();
                    await closing.ServiceProvider.GetRequiredService<Application.Organizations.OrganisationExitService>().CloseDueAsync(stoppingToken);
                    if (run.Failed > 0)
                    {
                        // R4: logged; the failed ones are tried again at the next run
                        _logger.LogWarning("Reminders: {Sent} sent, {Failed} could not be sent and are tried again", run.Sent, run.Failed);
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    _logger.LogError(error, "The reminders could not be sent");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
