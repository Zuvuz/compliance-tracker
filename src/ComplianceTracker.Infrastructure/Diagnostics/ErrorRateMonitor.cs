using Microsoft.Extensions.Logging;

namespace ComplianceTracker.Infrastructure.Diagnostics
{
    // US-098 R5: more than 10 errors within 5 minutes writes one "Error rate high" entry, which a log system can alert on.
    // It is written again only after a quiet window.
    public sealed partial class ErrorRateMonitor
    {
        public const int Threshold = 10;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

        private readonly Queue<DateTimeOffset> _errors = new();
        private readonly TimeProvider _clock;
        private readonly ILogger<ErrorRateMonitor> _log;
        private readonly object _lock = new();
        private DateTimeOffset? _reported;

        public ErrorRateMonitor(TimeProvider clock, ILogger<ErrorRateMonitor> log)
        {
            _clock = clock;
            _log = log;
        }

        // True when this error made the rate high (and it was logged)
        public bool Record()
        {
            int count;
            lock (_lock)
            {
                var now = _clock.GetUtcNow();
                _errors.Enqueue(now);
                while (_errors.Count > 0 && now - _errors.Peek() > Window)
                {
                    _errors.Dequeue();
                }

                count = _errors.Count;
                if (count <= Threshold || (_reported is { } at && now - at <= Window))
                {
                    return false;
                }

                _reported = now;
            }

            ErrorRateHigh(_log, count, (int)Window.TotalMinutes);
            return true;
        }

        [LoggerMessage(EventId = 5001, Level = LogLevel.Error, Message = "Error rate high: {Count} errors in the last {Minutes} minutes")]
        private static partial void ErrorRateHigh(ILogger logger, int count, int minutes);
    }
}
