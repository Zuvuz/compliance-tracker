using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Specs.Support
{
    public class InMemoryActivityLogRepository : IActivityLogRepository
    {
        private readonly List<ActivityLogEntry> _entries = new();

        public IReadOnlyList<ActivityLogEntry> Entries => _entries;

        public Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
