using System.Net;
using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Specs.Support
{
    // US-030: a pretend dev.azure.com for one scenario. The app's Azure DevOps adapter talks to it, so the tests never use the
    // network. Like Azure: a wrong token gives 401; an unknown organisation or project 404; only GET and the WIQL POST are
    // answered (the adapter only reads, R6).
    public sealed class FakeAzureDevOps
    {
        // "organisation/project" → the token it accepts and its work items
        public Dictionary<string, (string Token, List<FakeAzureItem> Items)> Projects { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Every request the adapter sent: method and path
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();

        public HttpMessageHandler CreateHandler() => new Handler(this);

        private HttpResponseMessage Answer(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath.Trim('/').Split('/').Select(Uri.UnescapeDataString).ToArray();
            lock (Requests)
            {
                Requests.Add((request.Method, request.RequestUri.AbsolutePath));
            }

            var token = request.Headers.Authorization is { Scheme: "Basic", Parameter: { } basic }
                ? Encoding.ASCII.GetString(Convert.FromBase64String(basic)).TrimStart(':')
                : null;

            // POST {org}/{project}/_apis/wit/wiql
            if (request.Method == HttpMethod.Post && path is [var org, var project, "_apis", "wit", "wiql"])
            {
                if (!Projects.TryGetValue($"{org}/{project}", out var found))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (found.Token != token)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                var query = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.GetProperty("query").GetString() ?? "";
                var ids = found.Items
                    .Where(i => query.Contains($"'{i.Type}'", StringComparison.Ordinal))
                    .Where(i => !query.Contains("[System.AreaPath] UNDER", StringComparison.Ordinal)
                                || query.Contains($"UNDER '{i.AreaPath}'", StringComparison.Ordinal))
                    .Select(i => new { id = i.Id });
                return Json(new { workItems = ids });
            }

            // GET {org}/_apis/wit/workitems?ids=…
            if (request.Method == HttpMethod.Get && path is [var organization, "_apis", "wit", "workitems"])
            {
                var match = Projects.Where(p => p.Key.StartsWith(organization + "/", StringComparison.OrdinalIgnoreCase)).ToList();
                if (match.Count == 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (match.All(p => p.Value.Token != token))
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
                var wanted = (query["ids"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
                var items = match.SelectMany(p => p.Value.Items).Where(i => wanted.Contains(i.Id)).Select(i => new
                {
                    id = i.Id,
                    fields = new Dictionary<string, object?>
                    {
                        ["System.Id"] = i.Id,
                        ["System.Title"] = i.Title,
                        ["System.WorkItemType"] = i.Type,
                        ["System.State"] = i.State,
                        ["System.Description"] = i.Description,
                        ["Microsoft.VSTS.Common.AcceptanceCriteria"] = i.AcceptanceCriteria,
                        ["System.Tags"] = i.Tags
                    }
                });
                return Json(new { count = wanted.Count, value = items });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(object body)
            => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

        private sealed class Handler : HttpMessageHandler
        {
            private readonly FakeAzureDevOps _azure;

            public Handler(FakeAzureDevOps azure)
            {
                _azure = azure;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_azure.Answer(request));
        }
    }

    public sealed class FakeAzureItem
    {
        public int Id { get; init; }
        public string Title { get; set; } = "";
        public string Type { get; init; } = "User Story";
        public string State { get; set; } = "New";
        public string Description { get; set; } = "";
        public string AcceptanceCriteria { get; set; } = "";
        public string Tags { get; set; } = "";
        public string AreaPath { get; init; } = "";
    }
}
