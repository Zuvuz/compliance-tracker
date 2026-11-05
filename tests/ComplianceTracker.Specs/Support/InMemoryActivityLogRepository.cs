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

        // US-101: only what the fast tests need; the real search is tested against PostgreSQL (ActivitySearch.feature)
        public Task<(IReadOnlyList<ActivityLogEntry> Entries, int Total)> SearchAsync(ActivityLogSearch search, CancellationToken cancellationToken = default)
        {
            var matches = _entries
                .Where(e => e.OrganizationId == search.OrganizationId && (search.SubjectId is null || e.SubjectId == search.SubjectId))
                .Reverse()
                .ToList();
            return Task.FromResult<(IReadOnlyList<ActivityLogEntry>, int)>((matches.Skip(search.Skip).Take(search.Take).ToList(), matches.Count));
        }
    }
}
