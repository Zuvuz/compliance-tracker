using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Specs.Support
{
    public class InMemoryActivityLogRepository : IActivityLogRepository
    {
        private readonly List<ActivityLogEntry> _entries = new();

        public IReadOnlyList<ActivityLogEntry> Entries => _entries;

        public void Append(ActivityLogEntry entry) => _entries.Add(entry);
    }
}