using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Mappings;

// #52: an upload of test results leaves a trace and says why it made no suggestions. Edge cases. Arrange / Act / Assert.
public class TestRunUploadTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();

    [Fact]
    public void An_upload_with_suggestions_says_what_it_did_and_no_why()
    {
        var upload = Upload(passed: 203, failed: 0, suggested: 12, blocked: 0, already: 0);

        Assert.Equal("203 passed · 12 suggested", upload.Summary);
        Assert.Null(upload.WhyNothing);
    }

    [Fact]
    public void Failing_and_blocked_and_already_suggested_are_all_in_the_summary()
    {
        var upload = Upload(passed: 5, failed: 1, suggested: 3, blocked: 1, already: 2);

        Assert.Equal("5 passed · 1 failed · 2 suggested · 1 blocked by failing tests · 2 already suggested from this file", upload.Summary);
    }

    [Fact]
    public void The_same_file_again_says_so()
        => Assert.StartsWith("The same file was uploaded before", Upload(3, 0, 0, 0, already: 1).WhyNothing);

    [Fact]
    public void Tags_of_a_framework_the_project_does_not_follow_name_it()
        => Assert.Equal("The tags name frameworks this project does not follow: iso27001 aiact. Add the framework to the project, or check the tags.",
            Upload(3, 0, 0, 0, 0, other: ["iso27001", "aiact"]).WhyNothing);

    [Fact]
    public void Unknown_codes_are_named_when_nothing_matched()
        => Assert.Equal("No tag matched a requirement. Unknown codes: @cra-13.", Upload(3, 0, 0, 0, 0, unknown: ["@cra-13"]).WhyNothing);

    [Fact]
    public void A_run_without_any_requirement_tag_says_so()
        => Assert.Equal("No test had a requirement tag such as @cra-I.2(d).", Upload(3, 0, 0, 0, 0).WhyNothing);

    [Fact]
    public void A_long_file_name_and_list_are_cut()
    {
        var upload = new TestRunUpload(Project, new string('a', 300) + ".trx", "sha", Anna, Monday, 1, 0, 0, 0, 0,
            Enumerable.Range(0, 1000).Select(i => $"@cra-X{i}"), []);

        Assert.Equal(200, upload.FileName.Length);
        Assert.Equal(TestRunUpload.MaxListLength, upload.UnknownTags.Length);
    }

    // ---------------------------------------------------------- A withdrawn suggestion comes back

    [Fact]
    public void Withdrawn_test_results_are_suggested_again_with_the_new_run()
    {
        var suggestion = EvidenceSuggestion.FromTestRun(Project, Guid.NewGuid(), "sha", "a.trx", ["x"], [], Monday);
        suggestion.Withdraw();

        suggestion.SuggestAgain("b.trx", ["x", "y"], [], Monday.AddDays(1));

        Assert.Equal(EvidenceSuggestionStatus.Pending, suggestion.Status);
        Assert.Equal(("b.trx", 2, Monday.AddDays(1)), (suggestion.ResultsFileName, suggestion.PassingCount, suggestion.CreatedAt));
    }

    [Fact]
    public void Only_a_withdrawn_suggestion_can_be_suggested_again()
    {
        var waiting = EvidenceSuggestion.FromTestRun(Project, Guid.NewGuid(), "sha", "a.trx", ["x"], [], Monday);

        Assert.Throws<DomainException>(() => waiting.SuggestAgain("a.trx", ["x"], [], Monday));
    }

    [Fact]
    public void A_failing_test_blocks_the_suggestion_made_again()
    {
        var suggestion = EvidenceSuggestion.FromTestRun(Project, Guid.NewGuid(), "sha", "a.trx", ["x"], [], Monday);
        suggestion.Withdraw();

        suggestion.SuggestAgain("a.trx", ["x"], ["y"], Monday);

        Assert.Equal(EvidenceSuggestionStatus.Blocked, suggestion.Status);
    }

    // ---------------------------------------------------------- The page's list

    [Fact]
    public void Every_upload_is_listed_and_what_waits_is_under_the_newest_of_its_file()
    {
        var requirement = Guid.NewGuid();
        var waiting = EvidenceSuggestion.FromTestRun(Project, requirement, "sha-a", "a.trx", ["x"], [], Monday);
        var first = Upload(1, 0, 1, 0, 0, sha: "sha-a", at: Monday);
        var again = Upload(1, 0, 0, 0, 1, sha: "sha-a", at: Monday.AddHours(1));
        var nothing = Upload(1, 0, 0, 0, 0, sha: "sha-b", at: Monday.AddHours(2));
        var names = new Dictionary<Guid, (string, string, string)> { [requirement] = ("I.2(d)", "Protection from unauthorised access", "cra") };

        var list = TestResultsUpload.From([waiting], [first, again, nothing], names);

        Assert.Equal([nothing, again, first], list.Select(u => u.Record));
        Assert.Empty(list[0].WaitingItems);
        Assert.Equal(("I.2(d)", "cra"), (list[1].WaitingItems.Single().Code, list[1].WaitingItems.Single().FrameworkSlug));
        Assert.Empty(list[2].WaitingItems);
    }

    [Fact]
    public void An_upload_from_before_uploads_were_kept_is_listed_while_something_waits()
    {
        var older = EvidenceSuggestion.FromTestRun(Project, Guid.NewGuid(), "sha-old", "old.trx", ["x"], [], Monday);

        var upload = Assert.Single(TestResultsUpload.From([older], [], new Dictionary<Guid, (string, string, string)>()));

        Assert.Null(upload.Record);
        Assert.Equal("?", upload.WaitingItems.Single().Code);
        Assert.Equal(1, upload.Acceptable);
    }

    [Fact]
    public void A_blocked_suggestion_waits_but_cannot_be_accepted_at_once()
    {
        var blocked = EvidenceSuggestion.FromTestRun(Project, Guid.NewGuid(), "sha", "a.trx", ["x"], ["y"], Monday);

        var upload = Assert.Single(TestResultsUpload.From([blocked], [Upload(1, 1, 1, 1, 0, sha: "sha", at: Monday)], new Dictionary<Guid, (string, string, string)>()));

        Assert.Equal((1, 0), (upload.Waiting, upload.Acceptable));
    }

    private static TestRunUpload Upload(int passed, int failed, int suggested, int blocked, int already,
        string[]? unknown = null, string[]? other = null, string sha = "sha", DateTime? at = null)
        => new(Project, "results.trx", sha, Anna, at ?? Monday, passed, failed, suggested, blocked, already, unknown ?? [], other ?? []);
}
