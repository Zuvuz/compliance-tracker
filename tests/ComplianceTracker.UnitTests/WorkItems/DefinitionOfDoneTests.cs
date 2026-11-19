using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.WorkItems;

// US-109: the Definition of Done, the ticks and the audit. Every test follows Arrange / Act / Assert.
public class DefinitionOfDoneTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static WorkItem Story(bool closed = false, string rules = "R1", string examples = "E1")
        => WorkItem.Create(Project, WorkItemSource.GitHub,
            new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", "[US-004] Sign in", rules, examples, "", "user-story", closed), Monday);

    private static (ProjectCompliance Compliance, Requirement Requirement) Linked(WorkItem story, bool withProof)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA demo", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        var requirement = framework.LatestVersion.AddRequirement("I.2(d)", "Access", null);
        var row = new RequirementCoverage(requirement, [new LinkedWorkItem(RequirementLink.Create(Project, requirement.Id, story.Id, Anna, Monday), story)])
        {
            Evidence = withProof ? [new EvidenceItem(Evidence.ForLink(Project, "Report", "https://example.com/r", null, Anna, null, Monday), false)] : []
        };
        return (new ProjectCompliance([new FrameworkCompliance(framework, framework.LatestVersion, [row])], [story]), requirement);
    }

    private static IReadOnlyList<DoneCriterion> Criteria(params DoneCheck[] checks)
        => checks.Select(c => new DoneCriterion(WorkItemType.UserStory, c, c == DoneCheck.Manual ? "Code reviewed" : DefinitionOfDone.TitleOf(c, WorkItemType.UserStory))).ToList();

    // ---------------------------------------------------------- The criteria (R1, R2, R8)

    [Fact]
    public void Every_kind_starts_from_the_defaults()
    {
        var definition = DefinitionOfDone.Default(Project);

        Assert.Equal("Rules and examples filled in, Linked to at least one requirement, Every linked requirement has evidence, Tests passed, Code reviewed",
            DefinitionOfDone.Text(definition.For(WorkItemType.UserStory)));
        Assert.Equal("Accepted by the product owner", definition.For(WorkItemType.UseCase).Last().Title);
        Assert.Equal(("Acceptance criteria filled in", "Verified"),
            (definition.For(WorkItemType.Requirement)[0].Title, definition.For(WorkItemType.Requirement).Last().Title));
        Assert.Equal(0, definition.Version);
    }

    [Fact]
    public void A_change_raises_the_version_and_says_what_it_was_and_became()
    {
        var definition = DefinitionOfDone.Default(Project);

        var change = definition.Change(WorkItemType.UserStory, [DoneCheck.Linked], ["Code reviewed", " Security reviewed ", "code reviewed"], Anna, null, Monday);

        Assert.Equal("Linked to at least one requirement, Code reviewed, Security reviewed", change!.Value.After);
        Assert.Equal(1, definition.Version);
    }

    [Fact]
    public void The_same_criteria_again_change_nothing()
    {
        var definition = DefinitionOfDone.Default(Project);

        Assert.Null(definition.Change(WorkItemType.Requirement, [DoneCheck.Linked, DoneCheck.TemplateFilled], ["Verified"], Anna, null, Monday));
        Assert.Equal(0, definition.Version);
    }

    [Fact]
    public void At_least_one_criterion_and_at_most_ten_manual_ones()
    {
        var definition = DefinitionOfDone.Default(Project);

        Assert.Equal("Keep at least one criterion", Assert.Throws<DomainException>(() => definition.Change(WorkItemType.UseCase, [], ["  "], Anna, null, Monday)).Message);
        Assert.Throws<DomainException>(() => definition.Change(WorkItemType.UseCase, [], Enumerable.Range(1, 11).Select(i => $"C{i}"), Anna, null, Monday));
        Assert.Throws<DomainException>(() => definition.Change(WorkItemType.UseCase, [], [new string('x', 121)], Anna, null, Monday));
    }

    // ---------------------------------------------------------- Finished (R5)

    [Fact]
    public void A_user_story_is_finished_when_it_is_closed_and_a_reopening_is_remembered()
    {
        var story = Story(closed: true);
        Assert.True(story.IsFinished);

        story.Update(new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", "[US-004] Sign in", "R1", "E1", "", "user-story", false), Monday.AddDays(1));

        Assert.Equal((false, Monday.AddDays(1)), (story.IsFinished, story.ReopenedAt));
    }

    [Fact]
    public void A_use_case_is_finished_when_a_person_marks_it_Done_and_a_story_cannot_be()
    {
        var useCase = WorkItem.FromDocument(Project, Guid.NewGuid(), WorkItemType.UseCase, new DocumentWorkItem("UC-12", "Sign in"), 1, Monday);

        useCase.MarkDone(Anna, Monday);

        Assert.Equal((true, Monday), (useCase.IsFinished, useCase.FinishedAt));
        Assert.Throws<DomainException>(() => useCase.MarkDone(Anna, Monday));
        Assert.Equal("A user story is done when it is closed in GitHub", Assert.Throws<DomainException>(() => Story().MarkDone(Anna, Monday)).Message);
        useCase.MarkNotDone();
        Assert.False(useCase.IsFinished);
    }

    // ---------------------------------------------------------- The checks (R2, R3)

    [Fact]
    public void Linked_with_proof_meets_and_without_proof_says_why()
    {
        var story = Story();
        var (proven, _) = Linked(story, withProof: true);
        var (unproven, _) = Linked(story, withProof: false);

        Assert.Equal(CriterionState.Met, DoneAudit.For(story, Criteria(DoneCheck.Evidence), proven, []).Criteria[0].State);
        var missing = DoneAudit.For(story, Criteria(DoneCheck.Evidence), unproven, []).Criteria[0];
        Assert.Equal((CriterionState.NotMet, "I.2(d): no evidence"), (missing.State, missing.Note));
    }

    [Fact]
    public void Not_linked_misses_both_link_and_evidence()
    {
        var story = Story();

        var row = DoneAudit.For(story, Criteria(DoneCheck.Linked, DoneCheck.Evidence), new ProjectCompliance([], [story]), []);

        Assert.All(row.Criteria, c => Assert.Equal((CriterionState.NotMet, "No requirement is linked"), (c.State, c.Note)));
    }

    [Fact]
    public void Tests_are_not_checked_until_test_results_are_uploaded()
    {
        var story = Story();
        var (compliance, _) = Linked(story, withProof: true);

        var row = DoneAudit.For(story, Criteria(DoneCheck.TestsPassed), compliance, []);

        Assert.Equal((CriterionState.NotChecked, true), (row.Criteria[0].State, row.Meets));
    }

    [Theory]
    [InlineData("", "E1", "Empty: rules")]
    [InlineData("R1", "", "Empty: examples")]
    [InlineData("", "", "Empty: rules, examples")]
    public void An_empty_template_section_is_named(string rules, string examples, string why)
    {
        var story = Story(rules: rules, examples: examples);

        var status = DoneAudit.For(story, Criteria(DoneCheck.TemplateFilled), null, []).Criteria[0];

        Assert.Equal((CriterionState.NotMet, why), (status.State, status.Note));
    }

    // ---------------------------------------------------------- Ticks (R4) and reopening

    [Fact]
    public void A_tick_meets_a_manual_criterion_until_it_is_unticked()
    {
        var story = Story();
        var tick = DoneTick.Tick(Project, story.Id, "Code reviewed", Anna, null, Monday);

        Assert.Equal(CriterionState.Met, DoneAudit.For(story, Criteria(DoneCheck.Manual), null, [tick]).Criteria[0].State);
        tick.Untick(Anna, Monday.AddHours(1));
        Assert.Equal(CriterionState.NotMet, DoneAudit.For(story, Criteria(DoneCheck.Manual), null, [tick]).Criteria[0].State);
        Assert.Throws<DomainException>(() => tick.Untick(Anna, Monday));
    }

    [Fact]
    public void A_tick_given_before_the_story_was_reopened_no_longer_counts()
    {
        var story = Story(closed: true);
        var before = DoneTick.Tick(Project, story.Id, "Code reviewed", Anna, null, Monday);
        story.Update(new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", "[US-004] Sign in", "R1", "E1", "", "user-story", false), Monday.AddDays(1));
        var after = DoneTick.Tick(Project, story.Id, "Code reviewed", Anna, null, Monday.AddDays(2));

        Assert.Equal((false, true), (before.CountsFor(story), after.CountsFor(story)));
    }

    // ---------------------------------------------------------- The audit (R6, R7)

    [Fact]
    public void The_export_has_a_line_per_finished_item_and_criterion_with_the_version()
    {
        var story = Story(closed: true);
        var definition = DefinitionOfDone.Default(Project);
        var row = DoneAudit.For(story, definition.For(WorkItemType.UserStory), null, []);
        var view = new DoneAuditView(null!, definition, [row, DoneAudit.For(Story(), definition.For(WorkItemType.UserStory), null, [])]);

        var csv = DoneAudit.Csv(view, new Dictionary<Guid, string>()).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(1 + definition.For(WorkItemType.UserStory).Count, csv.Length); // the unfinished story is left out
        Assert.Contains(csv, l => l.Contains("\"Code reviewed\",\"NotMet\",\"Not ticked\",\"0\""));
        Assert.Equal(1, view.FinishedNotMeeting);
    }
}
