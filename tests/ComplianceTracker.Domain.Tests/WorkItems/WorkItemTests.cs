using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Domain.Tests.WorkItems;

// Unit tests of WorkItem and the project's GitHub link (US-018).
// Every test follows Arrange / Act / Assert.
public class WorkItemTests
{
    private static readonly DateTime Monday = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    private static ExternalWorkItem Issue(bool closed = false, string title = "[US-003] Create organisation")
        => new("Zuvuz/compliance-tracker#19", "https://github.com/Zuvuz/compliance-tracker/issues/19",
            title, "- R1: works", "- R1: Anna", "Feature: x", "user-story", closed);

    private static Project CreateProject(LifecyclePhase phase = LifecyclePhase.Planning)
        => Project.Create(Guid.NewGuid(), "Acme Portal", null, "Web portal", null, phase);

    // ---------------------------------------------------------- WorkItem (R4, R8)

    [Fact]
    public void Create_stores_the_issue_as_an_open_user_story()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        // Act
        var item = WorkItem.Create(projectId, WorkItemSource.GitHub, Issue(), Monday);

        // Assert
        Assert.Equal(projectId, item.ProjectId);
        Assert.Equal(WorkItemType.UserStory, item.Type);
        Assert.Equal(WorkItemState.Open, item.State);
        Assert.Equal("- R1: works", item.Rules);
        Assert.Equal(Monday, item.FirstSyncedAt);
    }

    [Fact]
    public void Update_reports_a_change_only_when_something_changed()
    {
        // Arrange
        var item = WorkItem.Create(Guid.NewGuid(), WorkItemSource.GitHub, Issue(), Monday);

        // Act
        var same = item.Update(Issue(), Monday.AddHours(1));
        var renamed = item.Update(Issue(title: "[US-003] Renamed"), Monday.AddHours(2));

        // Assert
        Assert.False(same);
        Assert.True(renamed);
        Assert.Equal("[US-003] Renamed", item.Title);
        Assert.Equal(Monday.AddHours(2), item.LastSyncedAt);
    }

    [Fact]
    public void A_closed_issue_marks_the_work_item_closed()
    {
        // Arrange
        var item = WorkItem.Create(Guid.NewGuid(), WorkItemSource.GitHub, Issue(), Monday);

        // Act
        item.Update(Issue(closed: true), Monday);

        // Assert
        Assert.Equal(WorkItemState.Closed, item.State);
    }

    [Fact]
    public void A_removed_issue_is_marked_once_and_kept()
    {
        // Arrange
        var item = WorkItem.Create(Guid.NewGuid(), WorkItemSource.GitHub, Issue(), Monday);

        // Act
        var first = item.MarkRemoved();
        var second = item.MarkRemoved();

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.Equal(WorkItemState.Removed, item.State);
        Assert.Equal("[US-003] Create organisation", item.Title);
    }

    // ---------------------------------------------------------- GitHub link (R1)

    [Theory]
    [InlineData("Zuvuz/compliance-tracker", "Zuvuz/compliance-tracker")]
    [InlineData(" https://github.com/Zuvuz/compliance-tracker/ ", "Zuvuz/compliance-tracker")]
    public void The_repository_is_cleaned(string input, string expected)
    {
        // Arrange
        var project = CreateProject();

        // Act
        project.LinkGitHub(input, null, Guid.NewGuid(), Monday);

        // Assert
        Assert.Equal(expected, project.GitHub!.Repository);
        Assert.Equal(GitHubLink.DefaultLabel, project.GitHub.Label);
    }

    [Theory]
    [InlineData("compliance-tracker")]
    [InlineData("Zuvuz/compliance tracker")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    public void A_repository_that_is_not_owner_slash_name_is_refused(string input)
    {
        // Arrange
        var project = CreateProject();

        // Act
        var error = Assert.Throws<DomainException>(() => project.LinkGitHub(input, null, Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("Enter the repository as owner/name, e.g. Zuvuz/compliance-tracker", error.Message);
        Assert.Null(project.GitHub);
    }

    [Fact]
    public void Linking_the_same_repository_again_changes_nothing()
    {
        // Arrange
        var project = CreateProject();
        project.LinkGitHub("Zuvuz/compliance-tracker", "user-story", Guid.NewGuid(), Monday);

        // Act
        var changed = project.LinkGitHub("zuvuz/compliance-tracker", "user-story", Guid.NewGuid(), Monday.AddDays(1));

        // Assert
        Assert.False(changed);
        Assert.Equal(Monday, project.GitHub!.ConnectedAt);
    }

    [Fact]
    public void A_project_at_end_of_support_cannot_be_linked()
    {
        // Arrange
        var project = CreateProject(LifecyclePhase.EndOfSupport);

        // Act
        var error = Assert.Throws<DomainException>(() => project.LinkGitHub("Zuvuz/compliance-tracker", null, Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("A project at end of support cannot be changed", error.Message);
    }

    [Fact]
    public void A_failed_sync_keeps_the_last_good_time()
    {
        // Arrange
        var project = CreateProject();
        project.LinkGitHub("Zuvuz/compliance-tracker", null, Guid.NewGuid(), Monday);
        project.GitHub!.RecordSuccess(Monday, [new SkippedIssue("Zuvuz/compliance-tracker#41", "Draft", "no acceptance criteria")]);

        // Act
        project.GitHub.RecordFailure("Repository not found or private");

        // Assert
        Assert.Equal(Monday, project.GitHub.LastSyncedAt);
        Assert.Equal("Repository not found or private", project.GitHub.LastSyncError);
        Assert.Single(project.GitHub.Skipped);
    }
}
