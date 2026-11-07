using ComplianceTracker.Domain.Ai;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Scanning;
using System.Text;

namespace ComplianceTracker.Domain.Tests.EdgeCases;

// Edge cases of the domain added with US-110, US-111, US-060/061, US-078 and US-116: limits, repeated actions and
// wrong states. Every test follows Arrange / Act / Assert.
public class RecentStoriesEdgeCaseTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    // ---------------------------------------------------------- US-110: has the text changed?

    private static FrameworkVersion Version()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return Framework.Create(Guid.NewGuid(), "Security policy", CatalogFile.Create("policy.pdf", bytes), Anna, Monday).LatestVersion;
    }

    [Fact]
    public void A_change_in_upper_and_lower_case_is_a_change()
    {
        // Arrange
        var version = Version();
        var before = version.AddRequirement("A.1", "Passwords", null);
        var after = version.AddRequirement("A.2", "passwords", null);

        // Act
        var same = before.HasSameTextAs(after);

        // Assert
        Assert.False(same);
    }

    [Fact]
    public void No_description_and_an_empty_one_are_the_same()
    {
        // Arrange
        var version = Version();
        var before = version.AddRequirement("A.1", "Passwords", null);
        var after = version.AddRequirement("A.2", "Passwords", "   ");

        // Act
        var same = before.HasSameTextAs(after);

        // Assert
        Assert.True(same);
    }

    [Fact]
    public void Tabs_and_new_lines_count_as_spaces()
    {
        // Arrange
        var version = Version();
        var before = version.AddRequirement("A.1", "Passwords", "Long\tand\r\nunique");
        var after = version.AddRequirement("A.2", "Passwords", "Long and unique");

        // Act
        var same = before.HasSameTextAs(after);

        // Assert
        Assert.True(same);
    }

    [Fact]
    public void Evidence_and_decisions_carried_to_a_changed_text_wait_for_review_too()
    {
        // Arrange
        var evidence = EvidenceLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Anna, null, Monday);
        var decision = RequirementApplicability.NotApplicable(Guid.NewGuid(), Guid.NewGuid(), "No network", Anna, Monday);

        // Act
        var carriedEvidence = evidence.CarryTo(Guid.NewGuid(), textChanged: true);
        var carriedDecision = decision.CarryTo(Guid.NewGuid(), textChanged: true);

        // Assert
        Assert.True(carriedEvidence.NeedsReview);
        Assert.True(carriedDecision.NeedsReview);
        Assert.Equal("No network", carriedDecision.Reason);
    }

    [Fact]
    public void A_reviewed_decision_cannot_be_reviewed_again()
    {
        // Arrange
        var decision = RequirementApplicability.NotApplicable(Guid.NewGuid(), Guid.NewGuid(), "No network", Anna, Monday)
            .CarryTo(Guid.NewGuid(), textChanged: true);
        decision.MarkReviewed(Anna, Monday);

        // Act
        var error = Assert.Throws<DomainException>(() => decision.MarkReviewed(Anna, Monday));

        // Assert
        Assert.Equal("Nothing to review", error.Message);
    }

    // ---------------------------------------------------------- US-111: treatment actions

    private static Risk Risk() => ComplianceTracker.Domain.Risks.Risk.Create(Guid.NewGuid(), "Stolen sessions", null, 3, 4, Anna, RiskTreatment.Reduce, Anna, Monday);

    [Fact]
    public void An_action_title_of_exactly_200_characters_is_allowed()
    {
        // Arrange
        var risk = Risk();

        // Act
        var action = risk.AddAction(new string('a', RiskAction.MaxTitleLength), Anna, null, Anna, Monday);

        // Assert
        Assert.Equal(RiskAction.MaxTitleLength, action.Title.Length);
    }

    [Fact]
    public void An_action_title_of_201_characters_is_refused()
    {
        // Arrange
        var risk = Risk();

        // Act
        var error = Assert.Throws<DomainException>(() => risk.AddAction(new string('a', RiskAction.MaxTitleLength + 1), Anna, null, Anna, Monday));

        // Assert
        Assert.Equal("The title must be 200 characters or fewer", error.Message);
    }

    [Fact]
    public void The_same_story_cannot_be_linked_to_an_action_twice()
    {
        // Arrange
        var risk = Risk();
        var action = risk.AddAction("Shorter sessions", Anna, null, Anna, Monday);
        var story = Guid.NewGuid();
        risk.LinkAction(action.Id, story, null);

        // Act
        var error = Assert.Throws<DomainException>(() => risk.LinkAction(action.Id, story, null));

        // Assert
        Assert.Equal("It is already linked to the action", error.Message);
        Assert.Single(action.Links);
    }

    [Fact]
    public void A_link_needs_a_story_or_evidence()
    {
        // Arrange
        var risk = Risk();
        var action = risk.AddAction("Shorter sessions", Anna, null, Anna, Monday);

        // Act
        var error = Assert.Throws<DomainException>(() => risk.LinkAction(action.Id, null, null));

        // Assert
        Assert.Equal("Choose a user story or evidence", error.Message);
    }

    [Fact]
    public void An_action_cannot_be_done_twice()
    {
        // Arrange
        var risk = Risk();
        var action = risk.AddAction("Shorter sessions", Anna, null, Anna, Monday);
        risk.CompleteAction(action.Id, hasProof: true, Anna, Monday);

        // Act
        var error = Assert.Throws<DomainException>(() => risk.CompleteAction(action.Id, hasProof: true, Anna, Monday.AddDays(1)));

        // Assert
        Assert.Equal("The action is already done", error.Message);
        Assert.Equal(Monday, action.DoneAt);
    }

    [Fact]
    public void An_unknown_action_is_not_found()
    {
        // Arrange
        var risk = Risk();

        // Act
        var error = Assert.Throws<DomainException>(() => risk.LinkAction(Guid.NewGuid(), Guid.NewGuid(), null));

        // Assert
        Assert.Equal("Action not found", error.Message);
    }

    [Fact]
    public void An_action_due_today_is_not_overdue_yet()
    {
        // Arrange
        var today = new DateOnly(2026, 11, 6);
        var action = Risk().AddAction("Shorter sessions", Anna, today, Anna, Monday);

        // Act
        var overdue = action.IsOverdue(today);

        // Assert
        Assert.False(overdue);
        Assert.True(action.IsOverdue(today.AddDays(1)));
    }

    [Fact]
    public void An_action_without_a_due_date_is_never_overdue()
    {
        // Arrange
        var action = Risk().AddAction("Shorter sessions", Anna, null, Anna, Monday);

        // Act
        var overdue = action.IsOverdue(DateOnly.MaxValue);

        // Assert
        Assert.False(overdue);
    }

    [Fact]
    public void A_closed_risk_cannot_get_links_or_be_completed()
    {
        // Arrange
        var risk = Risk();
        var action = risk.AddAction("Shorter sessions", Anna, null, Anna, Monday);
        risk.Close("No longer relevant", Monday);

        // Act
        var link = Assert.Throws<DomainException>(() => risk.LinkAction(action.Id, Guid.NewGuid(), null));
        var done = Assert.Throws<DomainException>(() => risk.CompleteAction(action.Id, true, Anna, Monday));

        // Assert
        Assert.Equal("A closed risk cannot be changed", link.Message);
        Assert.Equal("A closed risk cannot be changed", done.Message);
    }

    // ---------------------------------------------------------- US-060: "not applicable" suggestions

    [Fact]
    public void A_very_long_reason_is_cut_to_1000_characters()
    {
        // Arrange (nothing)

        // Act
        var suggestion = ApplicabilitySuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('r', 5000), "", null, "m", "p", Monday);

        // Assert
        Assert.Equal(ApplicabilitySuggestion.MaxReasonLength, suggestion.Reason.Length);
    }

    [Fact]
    public void A_replaced_suggestion_cannot_be_accepted()
    {
        // Arrange
        var suggestion = ApplicabilitySuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "No network", "", null, "m", "p", Monday);
        suggestion.Replace();

        // Act
        var error = Assert.Throws<DomainException>(() => suggestion.Accept(Anna, Monday, null));

        // Assert
        Assert.Equal("The suggestion has already been decided", error.Message);
        Assert.Equal(SuggestionStatus.Replaced, suggestion.Status);
    }

    // ---------------------------------------------------------- US-078: scans and findings

    [Fact]
    public void An_empty_scan_file_is_refused()
    {
        // Arrange (nothing)

        // Act
        var error = Assert.Throws<DomainException>(() => Scan.Create(Guid.NewGuid(), "Trivy", "scan.sarif", [], Anna, Monday));

        // Assert
        Assert.Equal("The file is empty", error.Message);
    }

    [Fact]
    public void A_scan_of_exactly_50_MB_is_allowed_and_one_byte_more_is_not()
    {
        // Arrange
        var limit = new byte[Scan.MaxSizeInBytes];
        var over = new byte[Scan.MaxSizeInBytes + 1];

        // Act
        var scan = Scan.Create(Guid.NewGuid(), "Trivy", "scan.sarif", limit, Anna, Monday);
        var error = Assert.Throws<DomainException>(() => Scan.Create(Guid.NewGuid(), "Trivy", "scan.sarif", over, Anna, Monday));

        // Assert
        Assert.NotNull(scan);
        Assert.Equal("The scan is larger than 50 MB", error.Message);
    }

    [Theory]
    [InlineData("", "Unknown tool")]
    [InlineData("   ", "Unknown tool")]
    [InlineData(" Trivy ", "Trivy")]
    public void A_scan_without_a_tool_name_is_from_an_unknown_tool(string tool, string expected)
    {
        // Arrange (nothing)

        // Act
        var scan = Scan.Create(Guid.NewGuid(), tool, "scan.sarif", [1], Anna, Monday);

        // Assert
        Assert.Equal(expected, scan.Tool);
    }

    [Fact]
    public void A_scan_keeps_only_the_file_name_not_the_path()
    {
        // Arrange (nothing)

        // Act
        var scan = Scan.Create(Guid.NewGuid(), "Trivy", "../../etc/scan.sarif", [1], Anna, Monday);

        // Assert
        Assert.Equal("scan.sarif", scan.FileName);
    }

    [Fact]
    public void A_summary_without_critical_findings_does_not_mention_them()
    {
        // Arrange
        var scan = Scan.Create(Guid.NewGuid(), "Trivy", "scan.sarif", [1], Anna, Monday);

        // Act
        scan.Record(0, 2, 0, 0);

        // Assert
        Assert.Equal("Trivy: 0 new, 2 fixed, 0 open", scan.Summary);
    }

    [Fact]
    public void A_finding_without_rule_or_message_gets_placeholders_and_long_text_is_cut()
    {
        // Arrange (nothing)

        // Act
        var finding = Finding.Create(Guid.NewGuid(), "Trivy", "f", " ", FindingSeverity.Low, new string('m', 3000), null, Guid.NewGuid());

        // Assert
        Assert.Equal("(no rule)", finding.RuleId);
        Assert.Equal(2000, finding.Message.Length);
    }

    [Fact]
    public void A_finding_cannot_be_accepted_twice()
    {
        // Arrange
        var finding = Finding.Create(Guid.NewGuid(), "Trivy", "f", "CVE-1", FindingSeverity.High, "m", null, Guid.NewGuid());
        finding.Accept("Not reachable", Anna, Monday);

        // Act
        var error = Assert.Throws<DomainException>(() => finding.Accept("Again", Anna, Monday));

        // Assert
        Assert.Equal("The finding is already accepted", error.Message);
        Assert.Equal("Not reachable", finding.AcceptedReason);
    }

    [Fact]
    public void A_fixed_finding_that_is_still_missing_stays_fixed_from_the_first_time()
    {
        // Arrange
        var finding = Finding.Create(Guid.NewGuid(), "Trivy", "f", "CVE-1", FindingSeverity.High, "m", null, Guid.NewGuid());
        finding.MissingFrom(Guid.NewGuid(), Monday);

        // Act
        var changed = finding.MissingFrom(Guid.NewGuid(), Monday.AddDays(7));

        // Assert
        Assert.False(changed);
        Assert.Equal(Monday, finding.FixedAt);
    }

    // ---------------------------------------------------------- US-116: AI evaluations

    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(0, 0)]
    [InlineData(0.3333, 33.3)]
    [InlineData(1, 100)]
    [InlineData(1.7, 100)]
    public void Shares_are_kept_in_per_cent_between_0_and_100(double share, double percent)
    {
        // Arrange (nothing)

        // Act
        var run = AiEvaluation.ForMatching("m", "p", 1, null, null, share, share, share, share, "Ops", Monday);

        // Assert
        Assert.Equal(percent, run.Top1);
        Assert.Equal(percent, run.RejectedAboveLimit);
    }

    [Theory]
    [InlineData(new[] { 1 }, 1)]
    [InlineData(new[] { 5 }, 5)]
    [InlineData(new[] { 1, 2 }, 1.5)]
    [InlineData(new[] { 1, 1, 2 }, 1.33)]
    public void Grades_from_1_to_5_are_averaged_to_two_decimals(int[] grades, double average)
    {
        // Arrange (nothing)

        // Act
        var run = AiEvaluation.ForRationales("m", "p", grades, "Ops", Monday);

        // Assert
        Assert.Equal(average, run.AverageGrade);
    }

    // ---------------------------------------------------------- US-121: alerts from GitHub

    [Fact]
    public void A_finding_dismissed_in_the_source_is_accepted_without_a_person()
    {
        // Arrange
        var finding = Finding.Create(Guid.NewGuid(), "CodeQL", "github:code-scanning-7", "js/xss", FindingSeverity.High, "m", null, Guid.NewGuid());

        // Act
        var changed = finding.AcceptFromSource("Dismissed in GitHub: false positive", Monday);

        // Assert
        Assert.True(changed);
        Assert.Equal(FindingState.Accepted, finding.State);
        Assert.Null(finding.AcceptedById);
    }

    [Fact]
    public void A_finding_already_accepted_keeps_its_first_reason()
    {
        // Arrange
        var finding = Finding.Create(Guid.NewGuid(), "CodeQL", "f", "js/xss", FindingSeverity.High, "m", null, Guid.NewGuid());
        finding.Accept("Not reachable", Anna, Monday);

        // Act
        var changed = finding.AcceptFromSource("Dismissed in GitHub: false positive", Monday.AddDays(1));

        // Assert
        Assert.False(changed);
        Assert.Equal("Not reachable", finding.AcceptedReason);
        Assert.Equal(Anna, finding.AcceptedById);
    }

    [Fact]
    public void An_empty_source_reason_gets_a_default()
    {
        // Arrange
        var finding = Finding.Create(Guid.NewGuid(), "CodeQL", "f", "js/xss", FindingSeverity.High, "m", null, Guid.NewGuid());

        // Act
        finding.AcceptFromSource(" ", Monday);

        // Assert
        Assert.Equal("Dismissed in the source", finding.AcceptedReason);
    }

    [Fact]
    public void A_scan_without_a_person_and_with_a_source_can_be_made()
    {
        // Arrange (nothing)

        // Act
        var scan = Scan.Create(Guid.NewGuid(), "CodeQL", "github-code-scanning-codeql.sarif", [1], null, Monday, ScanSource.GitHubCodeScanning);

        // Assert
        Assert.Null(scan.UploadedById);
        Assert.Equal(ScanSource.GitHubCodeScanning, scan.Source);
        Assert.Equal(ScanSource.Upload, Scan.Create(Guid.NewGuid(), "Trivy", "t.sarif", [1], Anna, Monday).Source);
    }

    // ---------------------------------------------------------- US-130: leaving a project out of AI improvement

    private static ComplianceTracker.Domain.Projects.Project Project(
        ComplianceTracker.Domain.Projects.LifecyclePhase phase = ComplianceTracker.Domain.Projects.LifecyclePhase.Planning)
        => ComplianceTracker.Domain.Projects.Project.Create(Guid.NewGuid(), "Acme Portal", null, "Purpose", null, phase);

    [Fact]
    public void Leaving_a_project_out_keeps_who_and_when_and_the_same_choice_twice_changes_nothing()
    {
        // Arrange
        var project = Project();

        // Act
        var first = project.SetExcludedFromAiSharing(true, Anna, Monday);
        var again = project.SetExcludedFromAiSharing(true, Anna, Monday.AddDays(1));

        // Assert
        Assert.NotNull(first);
        Assert.Null(again);
        Assert.True(project.ExcludedFromAiSharing);
        Assert.Equal(Monday, project.AiSharingChangedAt);
    }

    [Fact]
    public void A_project_at_end_of_support_can_still_be_left_out()
    {
        // Arrange
        var project = Project(ComplianceTracker.Domain.Projects.LifecyclePhase.EndOfSupport);

        // Act
        var change = project.SetExcludedFromAiSharing(true, Anna, Monday);

        // Assert
        Assert.NotNull(change);
    }
}
