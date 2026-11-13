using ComplianceTracker.Infrastructure.Persistence;

namespace ComplianceTracker.Specs.Support
{
    // US-139: the number of database queries of the last request to each page
    public sealed class QueryCounts : IQueryCountObserver
    {
        private readonly Dictionary<string, int> _byPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public void Counted(string path, int queries)
        {
            lock (_lock)
            {
                _byPath[path] = queries;
            }
        }

        public int For(string path)
        {
            lock (_lock)
            {
                return _byPath.TryGetValue(path, out var count) ? count : throw new Xunit.Sdk.XunitException($"No request to '{path}' was counted.");
            }
        }
    }
}
