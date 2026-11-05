using System.Net;
using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Specs.Support
{
    // A pretend GitHub for one scenario (US-018). The web app's GitHub adapter talks to it instead of
    // api.github.com, so the tests never use the network and can change issues between syncs.
    // Reqnroll creates one per scenario and gives the same instance to WebScenario and the steps.
    public sealed class FakeGitHub
    {
        private readonly Dictionary<string, List<FakeIssue>> _repositories = new(StringComparer.OrdinalIgnoreCase);

        // Every request the adapter sent, e.g. to prove it never writes (R2)
        public List<HttpMethod> Requests { get; } = new();

        // Makes every request fail with this status, e.g. 403 for the rate limit
        public HttpStatusCode? FailWith { get; set; }

        // US-031: private repositories and the one token each accepts. Like GitHub: without a token 404, a wrong one 401.
        public Dictionary<string, string> PrivateRepositories { get; } = new(StringComparer.OrdinalIgnoreCase);

        // US-063: the token the app's feedback writer is given (TestApp), and the issues it created
        public const string FeedbackToken = "test-feedback-token";

        public List<CreatedFeedbackIssue> CreatedIssues { get; } = new();

        public List<FakeIssue> Repository(string name)
        {
            if (!_repositories.TryGetValue(name, out var issues))
            {
                issues = new List<FakeIssue>();
                _repositories[name] = issues;
            }

            return issues;
        }

        public FakeIssue Issue(int number)
            => _repositories.Values.SelectMany(i => i).Single(i => i.Number == number);

        // A new handler for every HttpClient the factory builds; they all share this GitHub's data
        public HttpMessageHandler CreateHandler() => new Handler(this);

        // An issue body as GitHub writes an issue made from .github/ISSUE_TEMPLATE/user-story.yml
        public static string TemplateBody(string title) => $"""
            ### Role

            Admin

            ### Rules

            - R1: {title} works

            ### Examples

            - R1: Anna tries it and it works

            ### Gherkin

            ```gherkin
            Feature: {title}
              Scenario: It works
                Given a user
            ```
            """;

        private HttpResponseMessage Answer(HttpRequestMessage request)
        {
            lock (this)
            {
                Requests.Add(request.Method);
                if (FailWith is { } status)
                {
                    return new HttpResponseMessage(status);
                }

                // repos/{owner}/{name}/issues?labels=...&state=all&per_page=100&page=1
                var path = request.RequestUri!.AbsolutePath.Trim('/').Split('/');

                // US-063: POST repos/{owner}/{name}/issues creates an issue, with the feedback token only
                if (request.Method == HttpMethod.Post && path.Length == 4 && path[0] == "repos" && path[3] == "issues")
                {
                    if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: FeedbackToken })
                    {
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    }

                    using var sent = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                    var number = 130 + CreatedIssues.Count;
                    CreatedIssues.Add(new CreatedFeedbackIssue(
                        $"{path[1]}/{path[2]}",
                        number,
                        sent.RootElement.GetProperty("title").GetString()!,
                        sent.RootElement.GetProperty("body").GetString()!,
                        sent.RootElement.GetProperty("labels").EnumerateArray().Select(l => l.GetString()!).ToList()));
                    return new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(new { number, html_url = $"https://github.com/{path[1]}/{path[2]}/issues/{number}" }),
                            Encoding.UTF8, "application/json")
                    };
                }
                if (request.Method != HttpMethod.Get || path.Length != 4 || path[0] != "repos" || path[3] != "issues"
                    || !_repositories.TryGetValue($"{path[1]}/{path[2]}", out var issues))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (PrivateRepositories.TryGetValue($"{path[1]}/{path[2]}", out var acceptedToken))
                {
                    var auth = request.Headers.Authorization;
                    if (auth is null)
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    }

                    if (auth.Scheme != "Bearer" || auth.Parameter != acceptedToken)
                    {
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    }
                }

                var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
                var page = int.Parse(query["page"] ?? "1");
                var label = query["labels"];
                var json = page > 1 ? "[]" : JsonSerializer.Serialize(issues
                    .Where(i => label is null || i.Labels.Contains(label))
                    .Select(i => new
                    {
                        number = i.Number,
                        title = i.Title,
                        body = i.Body,
                        state = i.State,
                        html_url = $"https://github.com/{path[1]}/{path[2]}/issues/{i.Number}",
                        labels = i.Labels.Select(name => new { name })
                    }));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly FakeGitHub _github;

            public Handler(FakeGitHub github)
            {
                _github = github;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_github.Answer(request));
        }
    }

    // US-063: an issue the feedback writer created
    public sealed record CreatedFeedbackIssue(string Repository, int Number, string Title, string Body, List<string> Labels);

    public sealed class FakeIssue
    {
        public int Number { get; init; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public List<string> Labels { get; set; } = new();
        public string State { get; set; } = "open";
    }
}
