using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;
using System.Text;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of the project's frameworks (US-011): use, change version and remove.
// Rules that need the database (which frameworks the organisation may use) are tested in the Specs project.
// Every test follows Arrange / Act / Assert.
public class ProjectFrameworkTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BoId = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tuesday = Monday.AddDays(1);

    private static CatalogFile PdfFile(byte marker = 0)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        bytes[^1] = marker;
        return CatalogFile.Create("policy.pdf", bytes);
    }

    private static Project CreateProject()
        => Project.Create(OrganizationId, "Acme Portal", null, "Web portal", null, LifecyclePhase.Planning);

    // "Security policy" with version 1 and version 2, both activated (US-007: projects only use active versions)
    private static Framework PolicyWithTwoVersions()
    {
        var framework = Framework.Create(OrganizationId, "Security policy", PdfFile(), AnnaId, Monday);
        framework.AddVersion(PdfFile(marker: 1), AnnaId, Monday);
        foreach (var version in framework.Versions)
        {
            version.AddRequirement("1.1", "Passwords", null);
            version.Activate(AnnaId, Monday);
        }

        return framework;
    }

    private static FrameworkVersion Version(Framework framework, int number)
        => framework.Versions.Single(v => v.Number == number);

    // ---------------------------------------------------------- UseFramework

    // US-007: a draft can still change, so a project cannot follow it
    [Fact]
    public void A_draft_version_cannot_be_used()
    {
        // Arrange
        var project = CreateProject();
        var framework = Framework.Create(OrganizationId, "Security policy", PdfFile(), AnnaId, Monday);

        // Act
        var exception = Record.Exception(() => project.UseFramework(framework, framework.LatestVersion, AnnaId, Monday));

        // Assert
        Assert.Equal("Only active versions can be used", Assert.IsType<DomainException>(exception).Message);
        Assert.Empty(project.Frameworks);
    }

    [Fact]
    public void A_new_project_uses_no_frameworks()
    {
        // Arrange (nothing)

        // Act
        var project = CreateProject();

        // Assert
        Assert.Empty(project.Frameworks);
    }

    [Fact]
    public void UseFramework_stores_the_version_who_and_when()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();

        // Act
        var link = project.UseFramework(framework, Version(framework, 1), AnnaId, Monday);

        // Assert
        Assert.Equal(project.Id, link.ProjectId);
        Assert.Equal(framework.Id, link.FrameworkId);
        Assert.Equal(Version(framework, 1).Id, link.FrameworkVersionId);
        Assert.Equal(AnnaId, link.AddedById);
        Assert.Equal(Monday, link.AddedAt);
        Assert.True(link.IsActive);
        Assert.Same(link, project.FrameworkInUse(framework.Id));
    }

    [Fact]
    public void A_project_cannot_use_two_versions_of_the_same_framework()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();
        project.UseFramework(framework, Version(framework, 2), AnnaId, Monday);

        // Act
        var exception = Record.Exception(() => project.UseFramework(framework, Version(framework, 1), AnnaId, Tuesday));

        // Assert
        var domainException = Assert.IsType<DomainException>(exception);
        Assert.Equal("The project already uses Security policy", domainException.Message);
        Assert.Single(project.Frameworks);
    }

    [Fact]
    public void A_version_of_another_framework_is_rejected()
    {
        // Arrange
        var project = CreateProject();
        var policy = PolicyWithTwoVersions();
        var other = Framework.Create(OrganizationId, "ISO/IEC 27001", PdfFile(marker: 2), AnnaId, Monday);

        // Act
        var exception = Record.Exception(() => project.UseFramework(policy, other.LatestVersion, AnnaId, Monday));

        // Assert
        Assert.IsType<DomainException>(exception);
        Assert.Empty(project.Frameworks);
    }

    // ---------------------------------------------------------- ChangeFrameworkVersion

    [Fact]
    public void ChangeFrameworkVersion_moves_the_project_and_keeps_the_old_link()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();
        var old = project.UseFramework(framework, Version(framework, 1), AnnaId, Monday);

        // Act
        var changed = project.ChangeFrameworkVersion(framework, Version(framework, 2), BoId, Tuesday);

        // Assert
        Assert.True(changed);
        Assert.Equal(Version(framework, 2).Id, project.FrameworkInUse(framework.Id)!.FrameworkVersionId);
        Assert.False(old.IsActive);
        Assert.Equal(BoId, old.RemovedById);
        Assert.Equal(Tuesday, old.RemovedAt);
        Assert.Equal(2, project.FrameworkHistory.Count);
    }

    [Fact]
    public void ChangeFrameworkVersion_to_the_same_version_changes_nothing()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();
        project.UseFramework(framework, Version(framework, 2), AnnaId, Monday);

        // Act
        var changed = project.ChangeFrameworkVersion(framework, Version(framework, 2), BoId, Tuesday);

        // Assert
        Assert.False(changed);
        Assert.Single(project.FrameworkHistory);
    }

    [Fact]
    public void ChangeFrameworkVersion_requires_the_framework_to_be_in_use()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();

        // Act
        var exception = Record.Exception(() => project.ChangeFrameworkVersion(framework, Version(framework, 2), AnnaId, Monday));

        // Assert
        Assert.IsType<DomainException>(exception);
    }

    // ---------------------------------------------------------- RemoveFramework

    [Fact]
    public void RemoveFramework_keeps_the_history()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();
        var link = project.UseFramework(framework, Version(framework, 1), AnnaId, Monday);

        // Act
        project.RemoveFramework(framework, BoId, Tuesday);

        // Assert
        Assert.Empty(project.Frameworks);
        Assert.Single(project.FrameworkHistory);
        Assert.Equal(BoId, link.RemovedById);
        Assert.Equal(Tuesday, link.RemovedAt);
    }

    [Fact]
    public void A_removed_framework_can_be_added_again()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();
        project.UseFramework(framework, Version(framework, 1), AnnaId, Monday);
        project.RemoveFramework(framework, AnnaId, Monday);

        // Act
        var exception = Record.Exception(() => project.UseFramework(framework, Version(framework, 2), AnnaId, Tuesday));

        // Assert
        Assert.Null(exception);
        Assert.Equal(Version(framework, 2).Id, project.FrameworkInUse(framework.Id)!.FrameworkVersionId);
    }

    [Fact]
    public void RemoveFramework_requires_the_framework_to_be_in_use()
    {
        // Arrange
        var project = CreateProject();
        var framework = PolicyWithTwoVersions();

        // Act
        var exception = Record.Exception(() => project.RemoveFramework(framework, AnnaId, Monday));

        // Assert
        Assert.IsType<DomainException>(exception);
    }
}
