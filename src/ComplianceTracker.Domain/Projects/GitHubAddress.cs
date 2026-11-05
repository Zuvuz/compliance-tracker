using ComplianceTracker.Domain.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Domain.Projects
{
    // US-107: what a GitHub address points at: a repository ("acme/portal"), an organisation's GitHub Project
    // ("orgs/acme/projects/3") or an organisation ("acme"), with or without "https://github.com/" in front
    public partial record GitHubAddress(GitHubAddressKind Kind, string Owner, string? Repository, int? ProjectNumber)
    {
        // GitHub's rules for owner and repository names
        [GeneratedRegex(@"^[A-Za-z0-9-]{1,39}$")]
        private static partial Regex OwnerFormat();

        [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
        private static partial Regex RepositoryFormat();

        public static GitHubAddress Parse(string? address)
        {
            var value = (address ?? "").Trim().TrimEnd('/');
            foreach (var prefix in new[] { "https://", "http://", "www." })
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value[prefix.Length..];
                }
            }

            if (value.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            {
                value = value["github.com/".Length..];
            }

            var parts = value.Split('/');
            if (parts.Length == 4 && parts[0].Equals("orgs", StringComparison.OrdinalIgnoreCase)
                && parts[2].Equals("projects", StringComparison.OrdinalIgnoreCase)
                && OwnerFormat().IsMatch(parts[1])
                && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0)
            {
                return new GitHubAddress(GitHubAddressKind.Project, parts[1], null, number);
            }

            if (parts.Length == 2 && OwnerFormat().IsMatch(parts[0]) && RepositoryFormat().IsMatch(parts[1]))
            {
                return new GitHubAddress(GitHubAddressKind.Repository, parts[0], parts[1], null);
            }

            if (parts.Length == 1 && OwnerFormat().IsMatch(parts[0]))
            {
                return new GitHubAddress(GitHubAddressKind.Organization, parts[0], null, null);
            }

            throw new DomainException("Enter a repository (owner/name), a GitHub Project (github.com/orgs/<org>/projects/<number>) or an organisation (github.com/<org>)");
        }
    }

    public enum GitHubAddressKind
    {
        Repository,
        Project,
        Organization
    }
}
