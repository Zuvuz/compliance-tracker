using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Infrastructure.WorkItems
{
    // US-030: Azure Boards on dev.azure.com, read-only (R6). A WIQL query finds the user stories (User Story, Product Backlog
    // Item and Requirement, R4), then a batch read gets their fields (R5). The token is a Personal Access Token with
    // "Work Items: Read", sent as Basic authentication with an empty user name; it is never logged.
    public sealed partial class AzureDevOpsWorkItemSource : IWorkItemSource
    {
        public const string Refused = "The token was refused by Azure DevOps";
        public const string ApiVersion = "7.1";
        private const int BatchSize = 200;

        // R4: the types that are user stories in the Agile, Scrum and CMMI processes
        public static readonly IReadOnlyList<string> StoryTypes = ["User Story", "Product Backlog Item", "Requirement"];

        private static readonly string[] Fields =
        [
            "System.Id", "System.Title", "System.Description", "Microsoft.VSTS.Common.AcceptanceCriteria",
            "System.WorkItemType", "System.State", "System.Tags"
        ];

        private readonly HttpClient _http;

        public AzureDevOpsWorkItemSource(HttpClient http)
        {
            _http = http;
        }

        public WorkItemSource Source => WorkItemSource.AzureDevOps;

        public async Task<WorkItemReadResult> ReadAsync(WorkItemQuery query, CancellationToken cancellationToken = default)
        {
            var parts = query.Location.Split('/', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(query.Token))
            {
                throw new WorkItemSourceException("Enter the organisation, the project and a token");
            }

            var (organization, project) = (parts[0], parts[1]);
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + query.Token));

            // 1. WIQL: the ids of the user stories in the project (and area)
            var wiql = new { query = Wiql(project, query.AreaPath) };
            using var wiqlRequest = new HttpRequestMessage(HttpMethod.Post,
                $"{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/wit/wiql?api-version={ApiVersion}")
            {
                Content = JsonContent.Create(wiql)
            };
            wiqlRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            var ids = new List<int>();
            using (var response = await SendAsync(wiqlRequest, cancellationToken))
            {
                var answer = await response.Content.ReadFromJsonAsync<WiqlAnswer>(cancellationToken);
                ids.AddRange(answer?.WorkItems?.Select(w => w.Id) ?? []);
            }

            // 2. Their fields, at most 200 per request
            var items = new List<ExternalWorkItem>();
            var skipped = new List<SkippedIssue>();
            foreach (var batch in ids.Distinct().Chunk(BatchSize))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{Uri.EscapeDataString(organization)}/_apis/wit/workitems?ids={string.Join(",", batch)}&fields={string.Join(",", Fields)}&api-version={ApiVersion}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
                using var response = await SendAsync(request, cancellationToken);
                var answer = await response.Content.ReadFromJsonAsync<BatchAnswer>(cancellationToken);
                foreach (var item in answer?.Value ?? [])
                {
                    var read = ToWorkItem(organization, project, item);
                    if (read.Item is not null)
                    {
                        items.Add(read.Item);
                    }
                    else
                    {
                        skipped.Add(read.Skipped!);
                    }
                }
            }

            return new WorkItemReadResult(items, skipped);
        }

        // R4: e.g. SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'Portal' AND [System.WorkItemType] IN (…)
        public static string Wiql(string project, string? areaPath)
        {
            static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
            var text = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = {Quote(project)} "
                + $"AND [System.WorkItemType] IN ({string.Join(", ", StoryTypes.Select(Quote))})";
            if (!string.IsNullOrWhiteSpace(areaPath))
            {
                text += $" AND [System.AreaPath] UNDER {Quote(areaPath.Trim())}";
            }

            return text + " ORDER BY [System.Id]";
        }

        // R4, R5: an item with acceptance criteria, or the reason it is skipped
        public static (ExternalWorkItem? Item, SkippedIssue? Skipped) ToWorkItem(string organization, string project, AzureWorkItem item)
        {
            string Field(string name) => item.Fields is { } fields && fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "" : "";

            var reference = $"{organization}/{project}#{item.Id}";
            var title = Field("System.Title").Trim();
            var criteria = PlainText(Field("Microsoft.VSTS.Common.AcceptanceCriteria"));
            if (criteria.Length == 0)
            {
                return (null, new SkippedIssue(reference, title, "no acceptance criteria"));
            }

            var state = Field("System.State");
            var type = Field("System.WorkItemType");
            var tags = string.Join(", ", Field("System.Tags").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return (new ExternalWorkItem(reference, AzureDevOpsLink.WorkItemUrl(organization, project, item.Id), title, "", "", "",
                string.Join(", ", new[] { type, tags }.Where(t => t.Length > 0)), IsClosed(state))
            {
                Description = PlainText(Field("System.Description")),
                AcceptanceCriteria = criteria
            }, null);
        }

        // R7: the states that mean done in the Agile, Scrum and CMMI processes
        public static bool IsClosed(string state) => state is "Closed" or "Done" or "Resolved" or "Removed";

        // R5: Azure's HTML fields as plain text: line breaks for blocks, no tags, entities decoded
        public static string PlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return "";
            }

            var text = BlockBreak().Replace(html, "\n");
            text = ListItem().Replace(text, "\n- ");
            text = Tag().Replace(text, "");
            text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
            var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();
            return string.Join("\n", lines.Where(l => l.Length > 0));
        }

        // R2, R8: a refused token (401, 403, or 203 with a sign-in page) and a project that is not found are said in words
        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new WorkItemSourceException("Azure DevOps could not be reached. Try again later", error);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NonAuthoritativeInformation
                || response.Content.Headers.ContentType?.MediaType == "text/html")
            {
                response.Dispose();
                throw new WorkItemSourceException(Refused);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                throw new WorkItemSourceException("The Azure DevOps organisation or project was not found");
            }

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new WorkItemSourceException($"Azure DevOps answered with an error ({status})");
            }

            return response;
        }

        [GeneratedRegex(@"<\s*(br|/p|/div|/h[1-6]|/li|/tr)\s*/?>", RegexOptions.IgnoreCase)]
        private static partial Regex BlockBreak();

        [GeneratedRegex(@"<\s*li[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex ListItem();

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex Tag();

        private sealed record WiqlAnswer(List<WiqlItem>? WorkItems);

        private sealed record WiqlItem(int Id);

        private sealed record BatchAnswer(List<AzureWorkItem>? Value);
    }

    public sealed record AzureWorkItem(int Id, Dictionary<string, JsonElement>? Fields);
}
