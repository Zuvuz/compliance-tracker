using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-110: review after a framework version change, through the Compliance tab.
    // UI contract: Views/Projects/_ComplianceTab.cshtml (per row .needs-review with .previous-text and .current-text,
    // the form .confirm-review, and .review-pending on links and evidence that wait for review).
    [Binding]
    public class LinkReviewStepDefinitions
    {
        private readonly WebScenario _web;

        public LinkReviewStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I mark {string} as still correct")]
        public async Task WhenIMarkAsStillCorrect(string code)
        {
            var form = Row(code).QuerySelector("form.confirm-review")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no \"Still correct\" button.");
            var fields = form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the row {string} needs no review")]
        public void ThenTheRowNeedsNoReview(string code)
            => Assert.Null(Row(code).QuerySelector(".needs-review"));

        [Then("the row {string} shows the old text {string} and the new text {string}")]
        public void ThenTheRowShowsTheOldAndNewText(string code, string oldText, string newText)
        {
            var review = Row(code).QuerySelector(".needs-review")
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' does not wait for review.");
            Assert.Equal(oldText, review.QuerySelector(".previous-text")?.TextContent.Trim());
            Assert.Equal(newText, review.QuerySelector(".current-text")?.TextContent.Trim());
        }

        [Then("the evidence {string} on the row {string} waits for review")]
        public void ThenTheEvidenceWaitsForReview(string title, string code)
        {
            var evidence = Row(code).QuerySelectorAll(".evidence").FirstOrDefault(e => e.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"The row '{code}' has no evidence '{title}'.");
            Assert.NotNull(evidence.QuerySelector(".review-pending"));
        }

        [Then("the review of {string} is logged as {string}")]
        public async Task ThenTheReviewIsLogged(string project, string details)
        {
            await using var db = _web.CreateDbContext();
            var projectId = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            Assert.Contains(details, await db.ActivityLog
                .Where(e => e.SubjectId == projectId && e.Action == ActivityAction.LinkReviewed)
                .Select(e => e.Details)
                .ToListAsync());
        }

        private IElement Row(string code)
            => _web.Page!.QuerySelector($"#requirements-table tr.requirement-row[data-code='{code}']")
               ?? throw new Xunit.Sdk.XunitException($"The page {_web.CurrentPath} has no requirement '{code}'.");
    }
}
