using System.Reflection;

namespace ComplianceTracker.Specs.Support
{
    // The folder with ComplianceTracker.slnx. Written into the test assembly when it is built (the .csproj, US-094),
    // so it is found wherever the tests run from; going up from the test's own folder is the fallback.
    public static class Repository
    {
        private const string SolutionFile = "ComplianceTracker.slnx";

        public static string Root()
        {
            var built = typeof(Repository).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "RepositoryRoot")?.Value;
            if (built is not null && File.Exists(Path.Combine(built, SolutionFile)))
            {
                return built;
            }

            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException($"Could not find the repository root ({SolutionFile}).");
        }
    }
}
