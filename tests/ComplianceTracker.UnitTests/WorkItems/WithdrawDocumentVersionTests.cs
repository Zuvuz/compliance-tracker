using System.Text;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.WorkItems;

// US-177: withdrawing the newest version of a requirement specification. Edge cases. Arrange / Act / Assert.
public class WithdrawDocumentVersionTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    [Fact]
    public void Only_the_newest_version_can_be_withdrawn_and_the_previous_is_current_again()
    {
        var (document, first, second) = TwoVersions();

        Assert.Equal("Only the newest version can be withdrawn",
            Assert.Throws<DomainException>(() => document.Withdraw(first.Id, "Wrong", Anna, Monday)).Message);

        var previous = document.Withdraw(second.Id, "Wrong file", Anna, Monday.AddDays(2));

        Assert.Same(first, previous);
        Assert.Same(first, document.Latest);
        Assert.True(second.IsWithdrawn);
        Assert.Equal(3, document.NextVersionNumber); // the numbers are never used twice
    }

    [Fact]
    public void Withdrawing_the_only_version_leaves_none()
    {
        var document = RequirementDocument.Create(Guid.NewGuid(), "kravspec.csv", Monday);
        var only = document.AddVersion(File(), new ImportCounts(1, 0, 0), [], Anna, Monday);

        Assert.Null(document.Withdraw(only.Id, "Wrong project", Anna, Monday));
        Assert.Null(document.Latest);
    }

    [Fact]
    public void A_version_needs_a_reason()
    {
        var (document, _, second) = TwoVersions();

        Assert.Throws<DomainException>(() => document.Withdraw(second.Id, " ", Anna, Monday));
        Assert.False(second.IsWithdrawn);
    }

    [Fact]
    public void A_version_is_used_by_a_report_locked_while_it_was_the_newest()
    {
        var (document, first, second) = TwoVersions();

        Assert.True(document.UsedByReportLockedAt(first, Monday.AddHours(12)));
        Assert.False(document.UsedByReportLockedAt(first, Monday.AddDays(2))); // the second was newer by then
        Assert.False(document.UsedByReportLockedAt(second, Monday.AddHours(12))); // locked before it came
        Assert.True(document.UsedByReportLockedAt(second, Monday.AddDays(2)));
    }

    [Fact]
    public void A_story_only_the_withdrawn_version_brought_is_removed()
    {
        var story = Story("REQ-020", version: 2, at: Monday.AddDays(1));

        Assert.True(story.UndoDocumentVersion(2, 1, Monday));

        Assert.Equal(WorkItemState.Removed, story.State);
        Assert.Equal(2, story.RemovedInVersion);
    }

    [Fact]
    public void A_story_the_withdrawn_version_removed_is_back_and_one_it_changed_belongs_to_the_previous_version()
    {
        var removed = Story("REQ-005", version: 1, at: Monday.AddMinutes(-1));
        removed.MarkRemovedIn(2);
        var changed = Story("REQ-003", version: 1, at: Monday.AddMinutes(-1));
        changed.UpdateFromDocument(new DocumentWorkItem("REQ-003", "Log in with MitID"), 2, Monday.AddDays(1));

        Assert.False(removed.UndoDocumentVersion(2, 1, Monday));
        Assert.False(changed.UndoDocumentVersion(2, 1, Monday));

        Assert.Equal((WorkItemState.Open, (int?)null), (removed.State, removed.RemovedInVersion));
        Assert.Equal(1, changed.DocumentVersion);
        Assert.Equal(WorkItemState.Open, changed.State);
    }

    [Fact]
    public void Withdrawing_the_first_version_removes_every_story_it_brought()
    {
        var story = Story("REQ-001", version: 1, at: Monday);

        Assert.True(story.UndoDocumentVersion(1, null, null));
        Assert.Equal(WorkItemState.Removed, story.State);
    }

    private static (RequirementDocument Document, RequirementDocumentVersion First, RequirementDocumentVersion Second) TwoVersions()
    {
        var document = RequirementDocument.Create(Guid.NewGuid(), "kravspec.csv", Monday);
        var first = document.AddVersion(File(), new ImportCounts(2, 0, 0), [], Anna, Monday);
        var second = document.AddVersion(File(), new ImportCounts(1, 1, 1), [], Anna, Monday.AddDays(1));
        return (document, first, second);
    }

    private static RequirementFile File() => RequirementFile.Create("kravspec.csv", Encoding.UTF8.GetBytes("ID,Title\nREQ-1,Log in\n"));

    private static WorkItem Story(string reference, int version, DateTime at)
        => WorkItem.FromDocument(Guid.NewGuid(), Guid.NewGuid(), WorkItemType.Requirement, new DocumentWorkItem(reference, "Log in"), version, at);
}
