using ComplianceTracker.Application.Feedback;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ComplianceTracker.Infrastructure.Feedback
{
    // US-063 R6: where feedback becomes issues. The token is a fine-grained token with only "Issues: read and write" on
    // that one repository. It comes from configuration (the environment variable Feedback__GitHubToken), never the repo.
    public class FeedbackOptions
    {
        public const string Section = "Feedback";

        public string Repository { get; set; } = "Zuvuz/compliance-tracker";

        public string? GitHubToken { get; set; }
    }

    // The one place the tracker writes to GitHub (B42, an exception to B12): a POST that creates an issue in the
    // tracker's own repository, when the system admin asks for it.
    public class GitHubFeedbackIssueWriter : IFeedbackIssueWriter
    {
        public const string Unreachable = "GitHub could not be reached; the feedback is kept";
        public const string NoToken = "No GitHub token for feedback is set up; the feedback is kept";

        private readonly HttpClient _http;
        private readonly FeedbackOptions _options;

        public GitHubFeedbackIssueWriter(HttpClient http, IOptions<FeedbackOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task<CreatedIssue> CreateAsync(string title, string body, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.GitHubToken))
            {
                throw new FeedbackIssueException(NoToken);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"repos/{_options.Repository}/issues")
            {
                Content = JsonContent.Create(new { title, body, labels })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.GitHubToken);

            try
            {
                using var response = await _http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new FeedbackIssueException(Unreachable);
                }

                var issue = await response.Content.ReadFromJsonAsync<Issue>(cancellationToken)
                    ?? throw new FeedbackIssueException(Unreachable);
                return new CreatedIssue(issue.Number, issue.HtmlUrl);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                throw new FeedbackIssueException(Unreachable, e);
            }
        }

        private sealed record Issue(
            [property: JsonPropertyName("number")] int Number,
            [property: JsonPropertyName("html_url")] string HtmlUrl);
    }
}
