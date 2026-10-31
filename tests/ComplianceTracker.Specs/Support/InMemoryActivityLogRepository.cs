using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Specs.Support
{
    // A fake activity log kept in a list, for the fast US-001 tests that do not use a database
    public class InMemoryActivityLogRepository : IActivityLogRepository
    {
        private readonly List<ActivityLogEntry> _entries = new();

        // Lets the tests read what was logged
        public IReadOnlyList<ActivityLogEntry> Entries => _entries;

        public Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ActivityLogEntry>> ListAboutAsync(Guid organizationId, Guid subjectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActivityLogEntry>>(_entries
                .Where(e => e.OrganizationId == organizationId && e.SubjectId == subjectId)
                .Reverse()
                .ToList());
    }
}
