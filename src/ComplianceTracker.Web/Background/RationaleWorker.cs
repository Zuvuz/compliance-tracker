using ComplianceTracker.Application.Ai;

namespace ComplianceTracker.Web.Background
{
    // US-020 step 2: writes the rationales of new AI suggestions in the background, one at a time, because a small
    // model on a laptop CPU takes seconds each. Woken when suggestions are made (IRationaleQueue), and also every
    // half minute, so nothing is lost after a restart or while the model was down.
    public class RationaleWorker : BackgroundService, IRationaleQueue
    {
        private const int BatchSize = 5;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<RationaleWorker> _logger;
        private readonly SemaphoreSlim _signal = new(0);

        public RationaleWorker(IServiceScopeFactory scopes, ILogger<RationaleWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        public void Wake()
        {
            // At most one waiting signal is needed
            if (_signal.CurrentCount == 0)
            {
                _signal.Release();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(Interval, stoppingToken);
                    await WriteAllAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (AiUnavailableException error)
                {
                    // The model is down; the suggestions keep "Writing rationale…" and are tried again later
                    _logger.LogWarning("Rationales are waiting: {Message}", error.Message);
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "Writing rationales failed");
                }
            }
        }

        private async Task WriteAllAsync(CancellationToken stoppingToken)
        {
            int written;
            do
            {
                // A new scope per batch, like a request: its own DbContext
                using var scope = _scopes.CreateScope();
                written = await scope.ServiceProvider.GetRequiredService<RationaleService>().WriteMissingAsync(BatchSize, stoppingToken);
            }
            while (written > 0 && !stoppingToken.IsCancellationRequested);
        }
    }
}
