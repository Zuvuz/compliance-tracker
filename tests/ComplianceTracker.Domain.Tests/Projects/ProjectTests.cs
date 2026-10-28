using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of Project.Create (US-008 R2): required fields, cleaning of text and the lifecycle phase.
// The rule that names are unique per organisation needs the database and is tested in the Specs project.
// Every test follows Arrange / Act / Assert.
public class ProjectTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    // A valid project; each test changes only the value it is about
    private static Project Create(
        string name = "Acme Portal",
        string? description = "Customer portal",
        string intendedPurpose = "Web portal where customers manage their orders",
        string? scope = "The web app and its API",
        LifecyclePhase phase = LifecyclePhase.Planning)
        => Project.Create(OrganizationId, name, description, intendedPurpose, scope, phase);

    // ---------------------------------------------------------- Valid values

    [Fact]
    public void Stores_the_given_values()
    {
        // Arrange
        const string name = "Acme Portal";
        const string purpose = "Web portal where customers manage their orders";

        // Act
        var project = Create(name: name, description: "Customer portal", intendedPurpose: purpose,
            scope: "The web app and its API", phase: LifecyclePhase.Development);

        // Assert
        Assert.Equal(OrganizationId, project.OrganizationId);
        Assert.Equal(name, project.Name);
        Assert.Equal("Customer portal", project.Description);
        Assert.Equal(purpose, project.IntendedPurpose);
        Assert.Equal("The web app and its API", project.Scope);
        Assert.Equal(LifecyclePhase.Development, project.Phase);
    }

    [Fact]
    public void Each_project_gets_its_own_id()
    {
        // Arrange
        var first = Create();

        // Act
        var second = Create();

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Removes_spaces_around_the_text_fields()
    {
        // Arrange
        const string padded = "  text  ";

        // Act
        var project = Create(name: padded, description: padded, intendedPurpose: padded, scope: padded);

        // Assert
        Assert.Equal("text", project.Name);
        Assert.Equal("text", project.Description);
        Assert.Equal("text", project.IntendedPurpose);
        Assert.Equal("text", project.Scope);
    }

    // Description and scope are optional: missing values become empty text, never null
    [Fact]
    public void Missing_description_and_scope_become_empty_text()
    {
        // Arrange
        string? noDescription = null;
        string? noScope = null;

        // Act
        var project = Create(description: noDescription, scope: noScope);

        // Assert
        Assert.Equal("", project.Description);
        Assert.Equal("", project.Scope);
    }

    [Theory]
    [InlineData(LifecyclePhase.Planning)]
    [InlineData(LifecyclePhase.Development)]
    [InlineData(LifecyclePhase.Released)]
    [InlineData(LifecyclePhase.EndOfSupport)]
    public void Accepts_every_lifecycle_phase(LifecyclePhase phase)
    {
        // Arrange: the phase comes from InlineData

        // Act
        var project = Create(phase: phase);

        // Assert
        Assert.Equal(phase, project.Phase);
    }

    // ---------------------------------------------------------- Required fields (R2)

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_an_empty_name(string name)
    {
        // Arrange: the name comes from InlineData

        // Act
        var error = Record.Exception(() => Create(name: name));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Project name is required", domainError.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n")]
    public void Rejects_an_empty_intended_purpose(string intendedPurpose)
    {
        // Arrange: the intended purpose comes from InlineData

        // Act
        var error = Record.Exception(() => Create(intendedPurpose: intendedPurpose));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Intended purpose is required", domainError.Message);
    }

    // When both are missing, the name is reported first, so the message is predictable
    [Fact]
    public void Reports_the_name_before_the_intended_purpose()
    {
        // Arrange
        const string emptyName = "";
        const string emptyPurpose = "";

        // Act
        var error = Record.Exception(() => Create(name: emptyName, intendedPurpose: emptyPurpose));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Project name is required", domainError.Message);
    }
}
