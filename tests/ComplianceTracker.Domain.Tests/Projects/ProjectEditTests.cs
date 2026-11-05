using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of Project.Update and Project.ChangePhase (US-024).
// The rule that names are unique per organisation needs the database and is tested in the Specs project.
// Every test follows Arrange / Act / Assert.
public class ProjectEditTests
{
    private static Project Create(LifecyclePhase phase = LifecyclePhase.Planning)
        => Project.Create(Guid.NewGuid(), "Acme Portal", "Customer portal", "Web portal", "Web app", phase);

    // ---------------------------------------------------------- R1: details

    [Fact]
    public void Update_stores_the_new_values_and_returns_what_changed()
    {
        // Arrange
        var project = Create();

        // Act
        var changes = project.Update(" Acme Customer Portal ", "Customer portal", "Web portal", "Web shop only");

        // Assert
        Assert.Equal("Acme Customer Portal", project.Name);
        Assert.Equal("Web shop only", project.Scope);
        Assert.Equal(
            ["Name: Acme Portal → Acme Customer Portal", "Scope: Web app → Web shop only"],
            changes.Select(c => c.ToString()));
    }

    [Fact]
    public void Update_with_the_same_values_changes_nothing()
    {
        // Arrange
        var project = Create();

        // Act
        var changes = project.Update("Acme Portal", "Customer portal", "Web portal", "Web app");

        // Assert
        Assert.Empty(changes);
    }

    [Fact]
    public void An_emptied_optional_field_is_shown_as_empty()
    {
        // Arrange
        var project = Create();

        // Act
        var changes = project.Update("Acme Portal", null, "Web portal", "Web app");

        // Assert
        Assert.Equal("Description: Customer portal → (empty)", Assert.Single(changes).ToString());
        Assert.Equal("", project.Description);
    }

    [Theory]
    [InlineData("", "Web portal", "Project name is required")]
    [InlineData("Acme Portal", "  ", "Intended purpose is required")]
    public void Update_has_the_same_required_fields_as_create(string name, string purpose, string message)
    {
        // Arrange
        var project = Create();

        // Act
        var error = Assert.Throws<DomainException>(() => project.Update(name, null, purpose, null));

        // Assert
        Assert.Equal(message, error.Message);
        Assert.Equal("Acme Portal", project.Name);
    }

    // ---------------------------------------------------------- R2: phase

    [Theory]
    [InlineData(LifecyclePhase.Planning, LifecyclePhase.Development)]
    [InlineData(LifecyclePhase.Released, LifecyclePhase.Development)] // backwards is allowed
    [InlineData(LifecyclePhase.Released, LifecyclePhase.EndOfSupport)]
    public void ChangePhase_moves_the_project(LifecyclePhase from, LifecyclePhase to)
    {
        // Arrange
        var project = Create(from);

        // Act
        var change = project.ChangePhase(to);

        // Assert
        Assert.Equal(to, project.Phase);
        Assert.Equal($"Phase: {from} → {to}", change!.ToString());
    }

    [Fact]
    public void ChangePhase_to_the_same_phase_changes_nothing()
    {
        // Arrange
        var project = Create(LifecyclePhase.Development);

        // Act
        var change = project.ChangePhase(LifecyclePhase.Development);

        // Assert
        Assert.Null(change);
    }

    [Fact]
    public void ChangePhase_refuses_an_unknown_phase()
    {
        // Arrange
        var project = Create();

        // Act
        var error = Assert.Throws<DomainException>(() => project.ChangePhase((LifecyclePhase)99));

        // Assert
        Assert.Equal("Unknown lifecycle phase", error.Message);
    }

    // ---------------------------------------------------------- R4: end of support is final

    [Fact]
    public void A_project_at_end_of_support_cannot_be_edited()
    {
        // Arrange
        var project = Create(LifecyclePhase.EndOfSupport);

        // Act
        var error = Assert.Throws<DomainException>(() => project.Update("New name", null, "Web portal", null));

        // Assert
        Assert.Equal("A project at end of support cannot be changed", error.Message);
        Assert.Equal("Acme Portal", project.Name);
    }

    [Fact]
    public void A_project_at_end_of_support_cannot_change_phase()
    {
        // Arrange
        var project = Create(LifecyclePhase.EndOfSupport);

        // Act
        var error = Assert.Throws<DomainException>(() => project.ChangePhase(LifecyclePhase.Released));

        // Assert
        Assert.Equal("A project at end of support cannot be changed", error.Message);
        Assert.True(project.IsClosed);
    }

    [Fact]
    public void A_project_at_end_of_support_cannot_change_its_frameworks()
    {
        // Arrange
        var project = Create(LifecyclePhase.EndOfSupport);
        var framework = Framework.Create(project.OrganizationId, "Security policy",
            CatalogFile.Create("policy.pdf", "%PDF-1.4 test"u8.ToArray()), Guid.NewGuid(), DateTime.UtcNow);

        // Act
        var error = Assert.Throws<DomainException>(
            () => project.UseFramework(framework, framework.LatestVersion, Guid.NewGuid(), DateTime.UtcNow));

        // Assert
        Assert.Equal("A project at end of support cannot be changed", error.Message);
    }
}
