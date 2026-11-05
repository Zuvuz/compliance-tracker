using ComplianceTracker.Domain.Feedback;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-063: the footer link (#send-feedback), the form /Feedback (#feedback-form) and the system admin's pages
    // /System/Feedback and /System/FeedbackItem/{id} (#create-feedback-issue, #dismiss-feedback, #feedback-status)
    [Binding]
    public class FeedbackStepDefinitions
    {
        // The smallest PNG signature plus some bytes; enough for the content check
        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

        private readonly WebScenario _web;
        private readonly FakeGitHub _github;

        public FeedbackStepDefinitions(WebScenario web, FakeGitHub github)
        {
            _web = web;
            _github = github;
        }

        // ---------------------------------------------------------- Given

        [Given("{string} has sent the feedback {string} with a screenshot")]
        public Task GivenHasSentTheFeedback(string user, string iWant)
            => SendAsync("Developer", iWant, "I see my own risks", "risks.png", Png);

        [Given("I am signed in as the system admin {string}")]
        public void GivenIAmSignedInAsTheSystemAdmin(string name)
        {
            _web.SignInAs(name);
            _web.Client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeader);
            _web.Client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "system-admin");
        }

        [Given("GitHub cannot be reached")]
        public void GivenGitHubCannotBeReached()
            => _github.FailWith = HttpStatusCode.ServiceUnavailable;

        // R9: written straight into the database, with the time of the scenario's clock
        [Given("{string} has sent {int} feedbacks in the last hour")]
        public async Task GivenHasSentFeedbacks(string user, int count)
        {
            for (var i = 0; i < count; i++)
            {
                await SendAsync("Developer", $"idea {i + 1}", "it is better", null, null);
            }
        }

        // ---------------------------------------------------------- When

        [When("I send the feedback {string} {string} {string} {string} {string} {string} with the screenshot {string}")]
        public Task WhenISendTheFeedback(string asALabel, string asA, string iWantLabel, string iWant, string soThatLabel, string soThat, string fileName)
            => SendAsync(asA, iWant, soThat, fileName, Png);

        [When("I send feedback with the screenshot {string} that is really text")]
        public Task WhenISendFeedbackWithText(string fileName)
            => SendAsync("Developer", "to filter the risks by owner", "I see my own risks", fileName, Encoding.UTF8.GetBytes("not an image"));

        [When("I create a GitHub issue from the feedback {string}")]
        public async Task WhenICreateAGitHubIssue(string iWant)
        {
            await OpenFeedbackAsync(iWant);
            var form = _web.Page!.QuerySelector("#create-feedback-issue")
                ?? throw new Xunit.Sdk.XunitException("The feedback has no \"Create GitHub issue\" form.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["title"] = form.QuerySelector("input[name=title]")!.GetAttribute("value")!,
                ["body"] = form.QuerySelector("textarea[name=body]")!.TextContent
            });
        }

        [When("I dismiss the feedback {string} with the reason {string}")]
        public async Task WhenIDismissTheFeedback(string iWant, string reason)
        {
            await OpenFeedbackAsync(iWant);
            var form = _web.Page!.QuerySelector("#dismiss-feedback")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string> { ["reason"] = reason });
        }

        // ---------------------------------------------------------- Then

        [Then("the footer has the link {string}")]
        public void ThenTheFooterHasTheLink(string text)
        {
            var link = _web.Page!.QuerySelector("footer a#send-feedback")
                ?? throw new Xunit.Sdk.XunitException("The footer has no feedback link.");
            Assert.Equal(text, link.TextContent.Trim());
            Assert.Contains("page=", link.GetAttribute("href"));
        }

        [Then("the feedback is stored with the screenshot and its SHA-256")]
        public async Task ThenTheFeedbackIsStored()
        {
            await using var db = _web.CreateDbContext();
            var feedback = await db.Feedback.SingleAsync();
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Png)).ToLowerInvariant(), feedback.ScreenshotSha256);
            Assert.Equal(Png, await db.FeedbackScreenshots.Where(s => s.FeedbackId == feedback.Id).Select(s => s.Content).SingleAsync());
        }

        [Then("GitHub has an issue with the labels {string}, {string} and {string}")]
        public void ThenGitHubHasAnIssue(string first, string second, string third)
            => Assert.Equal(new[] { first, second, third }, Assert.Single(_github.CreatedIssues).Labels);

        [Then("the issue does not mention {string} or {string}")]
        public void ThenTheIssueDoesNotMention(string first, string second)
        {
            var issue = Assert.Single(_github.CreatedIssues);
            foreach (var text in new[] { first, second })
            {
                Assert.DoesNotContain(text, issue.Title + issue.Body, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Then("the issue says {string}")]
        public void ThenTheIssueSays(string text)
            => Assert.Contains(text, Assert.Single(_github.CreatedIssues).Body);

        [Then("the feedback shows the issue's number")]
        public void ThenTheFeedbackShowsTheIssuesNumber()
            => Assert.Equal($"#{Assert.Single(_github.CreatedIssues).Number}", _web.Page!.QuerySelector("#feedback-issue")?.TextContent.Trim());

        [Then("the feedback is still {string}")]
        public async Task ThenTheFeedbackIsStill(string status)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(Enum.Parse<FeedbackStatus>(status), (await db.Feedback.SingleAsync()).Status);
        }

        // ---------------------------------------------------------- Helpers

        // Opens the form from the footer of the front page and sends it, like a user
        private async Task SendAsync(string asA, string iWant, string soThat, string? fileName, byte[]? content)
        {
            await _web.OpenAsync("/");
            await _web.OpenAsync(_web.Page!.QuerySelector("footer a#send-feedback")!.GetAttribute("href")!);

            using var form = new MultipartFormDataContent
            {
                { new StringContent(_web.AntiforgeryToken()), "__RequestVerificationToken" },
                { new StringContent(_web.Page!.QuerySelector("#feedback-form input[name=Page]")?.GetAttribute("value") ?? ""), "Page" },
                { new StringContent(asA), "AsA" },
                { new StringContent(iWant), "IWant" },
                { new StringContent(soThat), "SoThat" }
            };
            if (fileName is not null && content is not null)
            {
                var file = new ByteArrayContent(content);
                file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(file, "screenshot", fileName);
            }

            _web.Response = await _web.Client.PostAsync("/Feedback", form);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
        }

        private async Task OpenFeedbackAsync(string iWant)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Feedback.Where(f => f.IWant == iWant).Select(f => f.Id).SingleAsync();
            await _web.OpenAsync($"/System/FeedbackItem/{id}");
        }
    }
}
