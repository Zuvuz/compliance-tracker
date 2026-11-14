using ComplianceTracker.Application.Ai;

namespace ComplianceTracker.Web.Background
{
    // US-156 R2: runs the due "needs action" assessments every half minute, never during a request. Each request runs in
    // its own scope (IReassessmentScopes). Switched off with NeedsActionReassessment:Enabled = false (the tests run it
    // themselves).
    public class NeedsActionReassessmentWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NeedsActionReassessmentWorker> _logger;

        public NeedsActionReassessmentWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NeedsActionReassessmentWorker> logger)
        {
            _scopes = scopes;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_configuration.GetValue("NeedsActionReassessment:Enabled", true))
            {
                return;
            }

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<NeedsActionReassessmentJob>().RunDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "Assessing again what needs action failed");
                }
            }
        }
    }

    // A new scope per request, like a web request: its own DbContext and unit of work
    public class ReassessmentScopes : IReassessmentScopes
    {
        private readonly IServiceScopeFactory _scopes;

        public ReassessmentScopes(IServiceScopeFactory scopes)
        {
            _scopes = scopes;
        }

        public async Task<ReassessmentOutcome> RunAsync(
            Func<NeedsActionReassessmentRunner, CancellationToken, Task<ReassessmentOutcome>> work, CancellationToken cancellationToken = default)
        {
            using var scope = _scopes.CreateScope();
            return await work(scope.ServiceProvider.GetRequiredService<NeedsActionReassessmentRunner>(), cancellationToken);
        }
    }
}
