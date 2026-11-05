using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Projects;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComplianceTracker.Infrastructure.WorkItems
{
    // US-107: an organisation's GitHub Projects (boards) are only in GitHub's GraphQL API. Still only reading (B12): every
    // request is a query. Only the linked project's items are asked for (R2), and a token is always needed (R6).
    public partial class GitHubWorkItemSource : IGitHubProjects
    {
        private const string ListProjectsQuery = """
            query($login: String!) {
              organization(login: $login) {
                projectsV2(first: 100) { nodes { number title closed } }
              }
            }
            """;

        private const string ProjectItemsQuery = """
            query($login: String!, $number: Int!, $after: String) {
              organization(login: $login) {
                projectV2(number: $number) {
                  items(first: 100, after: $after) {
                    pageInfo { hasNextPage endCursor }
                    nodes {
                      content {
                        __typename
                        ... on Issue {
                          number title body state url
                          repository { nameWithOwner }
                          labels(first: 30) { nodes { name } }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        public async Task<IReadOnlyList<GitHubProjectInfo>> ListAsync(string organization, string token, CancellationToken cancellationToken = default)
        {
            var data = await GraphQlAsync<ListData>(ListProjectsQuery, new { login = organization }, token, cancellationToken);
            return (data.Organization?.ProjectsV2?.Nodes ?? [])
                .Where(p => !p.Closed)
                .OrderBy(p => p.Number)
                .Select(p => new GitHubProjectInfo(p.Number, p.Title))
                .ToList();
        }

        // R2: the issues on the one project, from whichever repositories they are in; pull requests and drafts are not stories
        private async Task<WorkItemReadResult> ReadProjectAsync(WorkItemQuery query, int number, CancellationToken cancellationToken)
        {
            if (query.Token is null)
            {
                throw new WorkItemSourceException("Reading a GitHub Project needs a token with read access to projects");
            }

            var items = new List<Domain.WorkItems.ExternalWorkItem>();
            var skipped = new List<SkippedIssue>();
            string? after = null;
            for (var page = 1; page <= MaxPages; page++)
            {
                var data = await GraphQlAsync<ItemsData>(ProjectItemsQuery, new { login = query.Location, number, after }, query.Token, cancellationToken);
                var project = data.Organization?.ProjectV2
                    ?? throw new WorkItemSourceException("GitHub Project not found, or the token cannot see it");

                foreach (var issue in project.Items.Nodes.Select(n => n.Content).Where(c => c?.Typename == "Issue"))
                {
                    var reference = $"{issue!.Repository!.NameWithOwner}#{issue.Number}";
                    var sections = BddIssueTemplate.Parse(issue.Body);
                    if (sections is null)
                    {
                        skipped.Add(new SkippedIssue(reference, issue.Title, NoAcceptanceCriteria));
                        continue;
                    }

                    items.Add(new Domain.WorkItems.ExternalWorkItem(
                        reference,
                        issue.Url,
                        issue.Title,
                        sections.Value.Rules,
                        sections.Value.Examples,
                        sections.Value.Gherkin,
                        string.Join(", ", issue.Labels?.Nodes.Select(l => l.Name) ?? []),
                        IsClosed: issue.State == "CLOSED"));
                }

                if (!project.Items.PageInfo.HasNextPage)
                {
                    break;
                }

                after = project.Items.PageInfo.EndCursor;
            }

            return new WorkItemReadResult(items, skipped);
        }

        private async Task<T> GraphQlAsync<T>(string query, object variables, string token, CancellationToken cancellationToken)
            where T : class
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "graphql") { Content = JsonContent.Create(new { query, variables }) };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new WorkItemSourceException("The token was refused by GitHub");
                }

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                {
                    throw new WorkItemSourceException("GitHub's limit for requests was reached. Try again later");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new WorkItemSourceException($"GitHub answered with an error ({(int)response.StatusCode})");
                }

                var answer = await response.Content.ReadFromJsonAsync<GraphQlAnswer<T>>(cancellationToken);
                // GraphQL answers 200 with "errors" when the organisation or project is not found or the token may not read it
                if (answer?.Errors is { Count: > 0 } || answer?.Data is null)
                {
                    throw new WorkItemSourceException("GitHub Project not found, or the token cannot see it");
                }

                return answer.Data;
            }
        }

        // ---------------------------------------------------------- GraphQL JSON

        private sealed class GraphQlAnswer<T>
            where T : class
        {
            [JsonPropertyName("data")] public T? Data { get; set; }
            [JsonPropertyName("errors")] public List<JsonElement>? Errors { get; set; }
        }

        private sealed class ListData
        {
            [JsonPropertyName("organization")] public ListOrganization? Organization { get; set; }
        }

        private sealed class ListOrganization
        {
            [JsonPropertyName("projectsV2")] public NodeList<ProjectNode>? ProjectsV2 { get; set; }
        }

        private sealed class ProjectNode
        {
            [JsonPropertyName("number")] public int Number { get; set; }
            [JsonPropertyName("title")] public string Title { get; set; } = "";
            [JsonPropertyName("closed")] public bool Closed { get; set; }
        }

        private sealed class ItemsData
        {
            [JsonPropertyName("organization")] public ItemsOrganization? Organization { get; set; }
        }

        private sealed class ItemsOrganization
        {
            [JsonPropertyName("projectV2")] public ProjectItems? ProjectV2 { get; set; }
        }

        private sealed class ProjectItems
        {
            [JsonPropertyName("items")] public ItemPage Items { get; set; } = new();
        }

        private sealed class ItemPage
        {
            [JsonPropertyName("pageInfo")] public PageInfo PageInfo { get; set; } = new();
            [JsonPropertyName("nodes")] public List<ItemNode> Nodes { get; set; } = [];
        }

        private sealed class PageInfo
        {
            [JsonPropertyName("hasNextPage")] public bool HasNextPage { get; set; }
            [JsonPropertyName("endCursor")] public string? EndCursor { get; set; }
        }

        private sealed class ItemNode
        {
            [JsonPropertyName("content")] public ItemContent? Content { get; set; }
        }

        private sealed class ItemContent
        {
            [JsonPropertyName("__typename")] public string? Typename { get; set; }
            [JsonPropertyName("number")] public int Number { get; set; }
            [JsonPropertyName("title")] public string Title { get; set; } = "";
            [JsonPropertyName("body")] public string? Body { get; set; }
            [JsonPropertyName("state")] public string? State { get; set; }
            [JsonPropertyName("url")] public string Url { get; set; } = "";
            [JsonPropertyName("repository")] public RepositoryName? Repository { get; set; }
            [JsonPropertyName("labels")] public NodeList<LabelNode>? Labels { get; set; }
        }

        private sealed class RepositoryName
        {
            [JsonPropertyName("nameWithOwner")] public string NameWithOwner { get; set; } = "";
        }

        private sealed class LabelNode
        {
            [JsonPropertyName("name")] public string Name { get; set; } = "";
        }

        private sealed class NodeList<T>
        {
            [JsonPropertyName("nodes")] public List<T> Nodes { get; set; } = [];
        }
    }
}
