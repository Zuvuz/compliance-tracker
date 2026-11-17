using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.UnitTests.Projects;

// US-028: archiving and restoring a project. Edge cases. Arrange / Act / Assert.
public class ArchiveProjectTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    [Fact]
    public void An_archived_project_keeps_who_and_when_and_is_closed()
    {
        var project = OldApp();

        project.Archive(Anna, Monday);

        Assert.True(project.IsArchived);
        Assert.True(project.IsClosed);
        Assert.Equal((Monday, Anna), (project.ArchivedAt!.Value, project.ArchivedById!.Value));
    }

    [Fact]
    public void An_archived_project_cannot_be_changed_and_says_why()
    {
        var project = OldApp();
        project.Archive(Anna, Monday);

        var error = Assert.Throws<DomainException>(() => project.Update("New name", null, "Purpose", null));

        Assert.Equal("An archived project cannot be changed", error.Message);
        Assert.Equal("Old App", project.Name);
    }

    [Fact]
    public void A_restored_project_can_be_changed_again()
    {
        var project = OldApp();
        project.Archive(Anna, Monday);

        project.Restore();

        Assert.False(project.IsArchived);
        Assert.False(project.IsClosed);
        Assert.Null(project.ArchivedById);
        project.Update("Old App 2", null, "Purpose", null);
        Assert.Equal("Old App 2", project.Name);
    }

    [Fact]
    public void Archiving_twice_or_restoring_what_is_not_archived_is_refused()
    {
        var project = OldApp();

        Assert.Equal("The project is not archived", Assert.Throws<DomainException>(project.Restore).Message);
        project.Archive(Anna, Monday);
        Assert.Equal("The project is already archived", Assert.Throws<DomainException>(() => project.Archive(Anna, Monday.AddDays(1))).Message);
        Assert.Equal(Monday, project.ArchivedAt); // the first time is kept
    }

    [Fact]
    public void The_organisation_area_cannot_be_archived()
    {
        var area = Project.CreateOrganisationArea(Guid.NewGuid());

        Assert.Throws<DomainException>(() => area.Archive(Anna, Monday));
        Assert.False(area.IsArchived);
    }

    private static Project OldApp() => Project.Create(Guid.NewGuid(), "Old App", null, "Purpose", null, LifecyclePhase.Development);
}
