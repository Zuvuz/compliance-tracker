using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ComplianceTracker.Infrastructure.WorkItems
{
    // Reads user stories from a public GitHub repository through GitHub's REST API, without a key (US-018).
    // Only GET requests: the tracker never writes to GitHub (R2, decision B12).
    // The HttpClient is set up in AddInfrastructure (base address, headers, timeout).
    // US-058: also reads which merged pull requests closed an issue (the issue's timeline), still only GET
    public partial class GitHubWorkItemSource : IWorkItemSource, Application.Compliance.IPullRequestReader
    {
        public const string NoAcceptanceCriteria = "no acceptance criteria";

        // 100 per page is GitHub's maximum. 10 pages = 1000 issues, enough for one project's stories.
        private const int PageSize = 100;
        private const int MaxPages = 10;

        private readonly HttpClient _http;

        public GitHubWorkItemSource(HttpClient http)
        {
            _http = http;
        }

        public WorkItemSource Source => WorkItemSource.GitHub;

        public async Task<WorkItemReadResult> ReadAsync(WorkItemQuery query, CancellationToken cancellationToken = default)
        {
            // US-107: an organisation's GitHub Project is read through GraphQL
            if (query.ProjectNumber is { } projectNumber)
            {
                return await ReadProjectAsync(query, projectNumber, cancellationToken);
            }

            var items = new List<ExternalWorkItem>();
            var skipped = new List<SkippedIssue>();

            for (var page = 1; page <= MaxPages; page++)
            {
                var issues = await PageAsync(query, page, cancellationToken);
                foreach (var issue in issues.Where(i => i.PullRequest is null)) // the issues API also returns pull requests
                {
                    var reference = $"{query.Location}#{issue.Number}";
                    var sections = BddIssueTemplate.Parse(issue.Body);
                    if (sections is null)
                    {
                        skipped.Add(new SkippedIssue(reference, issue.Title, NoAcceptanceCriteria));
                        continue;
                    }

                    items.Add(new ExternalWorkItem(
                        reference,
                        issue.HtmlUrl,
                        issue.Title,
                        sections.Value.Rules,
                        sections.Value.Examples,
                        sections.Value.Gherkin,
                        string.Join(", ", issue.Labels.Select(l => l.Name)),
                        IsClosed: issue.State == "closed"));
                }

                if (issues.Count < PageSize)
                {
                    break;
                }
            }

            return new WorkItemReadResult(items, skipped);
        }

        // US-058 R3: the merged pull requests that refer to the issue in its timeline ("cross-referenced"), e.g. with
        // "Closes #4". GitHub's REST API has no field for "closed by", so a merged pull request that mentions the issue counts.
        public async Task<IReadOnlyList<Application.Compliance.ClosingPullRequest>> ClosingPullRequestsAsync(
            string repository, int issueNumber, string? token, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{repository}/issues/{issueNumber}/timeline?per_page={PageSize}");
            if (token is not null)
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new WorkItemSourceException("GitHub could not be reached. Try again later", error);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new WorkItemSourceException($"GitHub answered with an error ({(int)response.StatusCode})");
                }

                var events = await response.Content.ReadFromJsonAsync<List<TimelineEvent>>(cancellationToken) ?? [];
                return events
                    .Where(e => e.Event == "cross-referenced" && e.Source?.Issue?.PullRequest?.MergedAt is not null)
                    .Select(e => e.Source!.Issue!)
                    .DistinctBy(i => i.Number)
                    .Select(i => new Application.Compliance.ClosingPullRequest(i.Number, i.Title, i.HtmlUrl))
                    .ToList();
            }
        }

        private sealed class TimelineEvent
        {
            [JsonPropertyName("event")] public string? Event { get; set; }
            [JsonPropertyName("source")] public TimelineSource? Source { get; set; }
        }

        private sealed class TimelineSource
        {
            [JsonPropertyName("issue")] public TimelineIssue? Issue { get; set; }
        }

        private sealed class TimelineIssue
        {
            [JsonPropertyName("number")] public int Number { get; set; }
            [JsonPropertyName("title")] public string Title { get; set; } = "";
            [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
            [JsonPropertyName("pull_request")] public TimelinePullRequest? PullRequest { get; set; }
        }

        private sealed class TimelinePullRequest
        {
            [JsonPropertyName("merged_at")] public DateTime? MergedAt { get; set; }
        }

        private async Task<List<GitHubIssue>> PageAsync(WorkItemQuery query, int page, CancellationToken cancellationToken)
        {
            // The repository was checked by GitHubLink (owner/name), and the label is escaped
            var path = $"repos/{query.Location}/issues?labels={Uri.EscapeDataString(query.Label)}&state=all&per_page={PageSize}&page={page}";

            // US-031: a private repository is read with the token. Set per request, never on the shared client.
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (query.Token is not null)
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", query.Token);
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new WorkItemSourceException("GitHub could not be reached. Try again later", error);
            }

            using (response)
            {
                // GitHub answers 404, not 401, for a private repository read without a token
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new WorkItemSourceException(query.Token is null
                        ? "Repository not found or private: add a token"
                        : "Repository not found, or the token cannot read it");
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new WorkItemSourceException("The token was refused by GitHub");
                }

                // Without a key GitHub allows 60 requests per hour per address
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                {
                    throw new WorkItemSourceException("GitHub's limit for requests was reached. Try again later");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new WorkItemSourceException($"GitHub answered with an error ({(int)response.StatusCode})");
                }

                return await response.Content.ReadFromJsonAsync<List<GitHubIssue>>(cancellationToken) ?? [];
            }
        }

        // The fields of GitHub's issue JSON that the tracker uses
        private sealed record GitHubIssue(
            [property: JsonPropertyName("number")] int Number,
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("body")] string? Body,
            [property: JsonPropertyName("state")] string State,
            [property: JsonPropertyName("html_url")] string HtmlUrl,
            [property: JsonPropertyName("labels")] List<GitHubLabel> Labels,
            [property: JsonPropertyName("pull_request")] object? PullRequest);

        private sealed record GitHubLabel([property: JsonPropertyName("name")] string Name);
    }
}
