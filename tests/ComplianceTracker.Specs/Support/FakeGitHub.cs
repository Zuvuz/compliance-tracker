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

        // US-107: organisations' GitHub Projects (boards), each with issues from any repository, and the token that may read them
        public const string ProjectsToken = "test-projects-token";

        public Dictionary<string, List<FakeProject>> Organizations { get; } = new(StringComparer.OrdinalIgnoreCase);

        // US-121: each repository's code scanning and Dependabot alerts, and sources that are switched off
        public Dictionary<string, List<FakeAlert>> CodeScanningAlerts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<FakeAlert>> DependabotAlerts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DependabotOff { get; } = new(StringComparer.OrdinalIgnoreCase);

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

                // US-107: POST /graphql, only for organisations' GitHub Projects, always with the projects token
                if (request.Method == HttpMethod.Post && path is ["graphql"])
                {
                    return GraphQl(request);
                }

                // US-121: repos/{owner}/{name}/code-scanning/alerts and repos/{owner}/{name}/dependabot/alerts?state=…&page=…
                if (request.Method == HttpMethod.Get && path.Length == 5 && path[0] == "repos" && path[4] == "alerts"
                    && path[3] is "code-scanning" or "dependabot")
                {
                    return Alerts($"{path[1]}/{path[2]}", path[3], request.RequestUri);
                }

                // US-058: repos/{owner}/{name}/issues/{number}/timeline, with the pull request that closed the issue
                if (request.Method == HttpMethod.Get && path.Length == 6 && path[0] == "repos" && path[3] == "issues" && path[5] == "timeline"
                    && _repositories.TryGetValue($"{path[1]}/{path[2]}", out var timelineIssues))
                {
                    var closedBy = timelineIssues.FirstOrDefault(i => i.Number.ToString() == path[4])?.ClosedBy;
                    var events = closedBy is null
                        ? "[]"
                        : JsonSerializer.Serialize(new[]
                        {
                            new
                            {
                                @event = "cross-referenced",
                                source = new
                                {
                                    issue = new
                                    {
                                        number = closedBy.Value.Number,
                                        title = closedBy.Value.Title,
                                        html_url = $"https://github.com/{path[1]}/{path[2]}/pull/{closedBy.Value.Number}",
                                        pull_request = new { merged_at = "2026-11-03T10:00:00Z" }
                                    }
                                }
                            }
                        });
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(events, Encoding.UTF8, "application/json") };
                }

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

        // US-121: GitHub's alert lists, one page; a switched-off Dependabot answers 403 like GitHub
        private HttpResponseMessage Alerts(string repository, string kind, Uri uri)
        {
            if (kind == "dependabot" && DependabotOff.Contains(repository))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{\"message\":\"Dependabot alerts are disabled for this repository.\"}", Encoding.UTF8, "application/json")
                };
            }

            var state = System.Web.HttpUtility.ParseQueryString(uri.Query)["state"] ?? "open";
            var page = int.TryParse(System.Web.HttpUtility.ParseQueryString(uri.Query)["page"], out var p) ? p : 1;
            var all = (kind == "dependabot" ? DependabotAlerts : CodeScanningAlerts).GetValueOrDefault(repository) ?? [];
            var alerts = page > 1 ? [] : all.Where(a => a.State == state).ToList();

            object body = kind == "dependabot"
                ? alerts.Select(a => new
                {
                    number = a.Number,
                    state = a.State,
                    dismissed_reason = a.DismissedReason,
                    dismissed_comment = (string?)null,
                    security_advisory = new { ghsa_id = $"GHSA-{a.Number}", cve_id = a.RuleId, summary = a.Message, severity = a.Severity },
                    security_vulnerability = new { severity = a.Severity, package = new { ecosystem = "npm", name = "left-pad" } },
                    dependency = new { manifest_path = "package-lock.json" }
                })
                : alerts.Select(a => new
                {
                    number = a.Number,
                    state = a.State,
                    dismissed_reason = a.DismissedReason,
                    dismissed_comment = (string?)null,
                    rule = new { id = a.RuleId, severity = "error", security_severity_level = a.Severity, description = a.Message },
                    tool = new { name = a.Tool },
                    most_recent_instance = new { message = new { text = a.Message }, location = new { path = "src/App.cs", start_line = 10 } }
                });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
        }

        // US-107: answers the two queries the adapter sends: the organisation's projects, and one project's items
        private HttpResponseMessage GraphQl(HttpRequestMessage request)
        {
            if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: ProjectsToken })
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            using var sent = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var query = sent.RootElement.GetProperty("query").GetString()!;
            var variables = sent.RootElement.GetProperty("variables");
            var login = variables.GetProperty("login").GetString()!;

            object answer;
            if (!Organizations.TryGetValue(login, out var projects))
            {
                answer = new { data = (object?)null, errors = new[] { new { type = "NOT_FOUND", message = "Could not resolve to an Organization" } } };
            }
            else if (query.Contains("projectsV2(", StringComparison.Ordinal))
            {
                answer = new { data = new { organization = new { projectsV2 = new { nodes = projects.Select(p => new { number = p.Number, title = p.Title, closed = false }) } } } };
            }
            else
            {
                var number = variables.GetProperty("number").GetInt32();
                var project = projects.FirstOrDefault(p => p.Number == number);
                answer = project is null
                    ? new { data = (object?)null, errors = new[] { new { type = "NOT_FOUND", message = "Could not resolve to a ProjectV2" } } }
                    : new
                    {
                        data = (object?)new
                        {
                            organization = new
                            {
                                projectV2 = new
                                {
                                    items = new
                                    {
                                        pageInfo = new { hasNextPage = false, endCursor = (string?)null },
                                        nodes = project.Items.Select(item => new
                                        {
                                            content = new Dictionary<string, object?>
                                            {
                                                ["__typename"] = "Issue",
                                                ["number"] = item.Issue.Number,
                                                ["title"] = item.Issue.Title,
                                                ["body"] = item.Issue.Body,
                                                ["state"] = item.Issue.State == "closed" ? "CLOSED" : "OPEN",
                                                ["url"] = $"https://github.com/{item.Repository}/issues/{item.Issue.Number}",
                                                ["repository"] = new { nameWithOwner = item.Repository },
                                                ["labels"] = new { nodes = item.Issue.Labels.Select(name => new { name }) }
                                            }
                                        })
                                    }
                                }
                            }
                        }
                    };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(answer), Encoding.UTF8, "application/json")
            };
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

    // US-107: a GitHub Project of an organisation, with its items: issues and the repository each is in
    public sealed class FakeProject
    {
        public int Number { get; init; }
        public string Title { get; init; } = "";
        public List<(string Repository, FakeIssue Issue)> Items { get; } = new();
    }

    // US-121: an alert of a repository; State is "open", "dismissed" or "fixed"
    public sealed class FakeAlert
    {
        public int Number { get; init; }
        public string Tool { get; init; } = "CodeQL";
        public string RuleId { get; init; } = "";
        public string Severity { get; init; } = "high";
        public string Message { get; init; } = "";
        public string State { get; set; } = "open";
        public string? DismissedReason { get; set; }
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

        // US-058: the merged pull request that closed the issue, if any
        public (int Number, string Title)? ClosedBy { get; set; }
    }
}
