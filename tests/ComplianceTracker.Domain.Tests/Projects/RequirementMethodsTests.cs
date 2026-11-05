using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of the methods a project uses to describe its requirements (US-104).
// Every test follows Arrange / Act / Assert.
public class RequirementMethodsTests
{
    private static Project NewProject()
        => Project.Create(Guid.NewGuid(), "Acme Portal", null, "A customer portal", null, LifecyclePhase.Development);

    [Fact]
    public void A_new_project_uses_every_method()
    {
        // Arrange / Act
        var project = NewProject();

        // Assert
        Assert.Equal(RequirementMethods.All, project.RequirementMethods);
    }

    [Fact]
    public void Changing_the_methods_returns_old_and_new()
    {
        // Arrange
        var project = NewProject();

        // Act
        var change = project.ChangeRequirementMethods(RequirementMethods.UseCases);

        // Assert
        Assert.Equal(RequirementMethods.UseCases, project.RequirementMethods);
        Assert.Equal("Methods: GitHub user stories, Requirement specification, Use cases → Use cases", change!.ToString());
    }

    [Fact]
    public void Choosing_the_same_methods_changes_nothing()
    {
        // Arrange
        var project = NewProject();

        // Act / Assert
        Assert.Null(project.ChangeRequirementMethods(RequirementMethods.All));
    }

    [Fact]
    public void At_least_one_method_is_needed()
    {
        // Arrange
        var project = NewProject();

        // Act / Assert
        var error = Assert.Throws<DomainException>(() => project.ChangeRequirementMethods(RequirementMethods.None));
        Assert.Equal("Choose at least one method", error.Message);
    }

    [Fact]
    public void Unknown_bits_are_ignored()
    {
        // Arrange
        var project = NewProject();

        // Act
        project.ChangeRequirementMethods((RequirementMethods)(8 | (int)RequirementMethods.GitHubUserStories));

        // Assert
        Assert.Equal(RequirementMethods.GitHubUserStories, project.RequirementMethods);
    }

    [Fact]
    public void A_project_at_end_of_support_cannot_change_its_methods()
    {
        // Arrange
        var project = NewProject();
        project.ChangePhase(LifecyclePhase.EndOfSupport);

        // Act / Assert
        Assert.Throws<DomainException>(() => project.ChangeRequirementMethods(RequirementMethods.UseCases));
    }
}
