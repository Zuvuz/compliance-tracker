using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using System.Text;

namespace ComplianceTracker.Domain.Tests.Frameworks;

// Unit tests of the draft, its requirements and activation (US-007).
// Every test follows Arrange / Act / Assert.
public class RequirementTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 1);

    private static CatalogFile PdfFile(byte marker = 0)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        bytes[^1] = marker;
        return CatalogFile.Create("iso27001.pdf", bytes);
    }

    private static Framework CreateFramework()
        => Framework.Create(OrganizationId, "ISO/IEC 27001", PdfFile(), AnnaId, Monday);

    // A draft with "A.5.15 Access control"
    private static FrameworkVersion DraftWithOneRequirement()
    {
        var version = CreateFramework().LatestVersion;
        version.AddRequirement("A.5.15", "Access control", "Rules for access");
        return version;
    }

    // ---------------------------------------------------------- Draft

    [Fact]
    public void A_new_version_is_a_draft_and_not_current()
    {
        // Arrange (nothing)

        // Act
        var framework = CreateFramework();

        // Assert
        Assert.Equal(FrameworkVersionStatus.Draft, framework.LatestVersion.Status);
        Assert.Null(framework.CurrentVersion(Today));
        Assert.Equal("ISO/IEC 27001 has no active version yet",
            Assert.Throws<DomainException>(() => framework.RequireCurrentVersion(Today)).Message);
    }

    [Fact]
    public void Requirements_keep_the_order_they_were_added_in()
    {
        // Arrange
        var version = CreateFramework().LatestVersion;

        // Act
        version.AddRequirement("A.5.16", "Identity management", null);
        version.AddRequirement(" A.5.15 ", " Access control ", null);

        // Assert
        Assert.Equal(["A.5.16", "A.5.15"], version.Requirements.Select(r => r.Code));
        Assert.Equal("Access control", version.Requirements[1].Title);
    }

    [Theory]
    [InlineData("", "Access control", "Requirement code is required")]
    [InlineData("A.5.17", " ", "Requirement title is required")]
    [InlineData("a.5.15", "Twice", "Requirement a.5.15 is already in the list")]
    public void A_requirement_needs_a_unique_code_and_a_title(string code, string title, string message)
    {
        // Arrange
        var version = DraftWithOneRequirement();

        // Act
        var error = Assert.Throws<DomainException>(() => version.AddRequirement(code, title, null));

        // Assert
        Assert.Equal(message, error.Message);
        Assert.Single(version.Requirements);
    }

    [Fact]
    public void A_requirement_in_a_draft_can_be_corrected()
    {
        // Arrange
        var version = DraftWithOneRequirement();
        var requirement = version.Requirements[0];

        // Act
        version.ChangeRequirement(requirement.Id, "A.5.15", "Access control policy", "");

        // Assert
        Assert.Equal("Access control policy", version.Requirements[0].Title);
        Assert.Equal("", version.Requirements[0].Description);
    }

    [Fact]
    public void A_correction_cannot_take_another_requirements_code()
    {
        // Arrange
        var version = DraftWithOneRequirement();
        var second = version.AddRequirement("A.5.16", "Identity management", null);

        // Act
        var error = Assert.Throws<DomainException>(() => version.ChangeRequirement(second.Id, "A.5.15", "X", null));

        // Assert
        Assert.Equal("Requirement A.5.15 is already in the list", error.Message);
    }

    [Fact]
    public void A_requirement_in_a_draft_can_be_removed()
    {
        // Arrange
        var version = DraftWithOneRequirement();

        // Act
        version.RemoveRequirement(version.Requirements[0].Id);

        // Assert
        Assert.Empty(version.Requirements);
    }

    // ---------------------------------------------------------- Activate (R3, R4)

    [Fact]
    public void Activating_locks_the_version_and_makes_it_current()
    {
        // Arrange
        var framework = CreateFramework();
        var version = framework.LatestVersion;
        version.AddRequirement("A.5.15", "Access control", null);

        // Act
        version.Activate(AnnaId, Monday);

        // Assert
        Assert.True(version.IsActive);
        Assert.Equal(Monday, version.ActivatedAt);
        Assert.Equal(AnnaId, version.ActivatedById);
        Assert.Same(version, framework.CurrentVersion(Today));
    }

    [Fact]
    public void An_active_version_cannot_be_changed()
    {
        // Arrange
        var version = DraftWithOneRequirement();
        version.Activate(AnnaId, Monday);
        var requirement = version.Requirements[0];

        // Act
        var errors = new[]
        {
            Assert.Throws<DomainException>(() => version.ChangeRequirement(requirement.Id, "A.5.15", "Changed", null)),
            Assert.Throws<DomainException>(() => version.AddRequirement("A.5.16", "New", null)),
            Assert.Throws<DomainException>(() => version.RemoveRequirement(requirement.Id)),
            Assert.Throws<DomainException>(() => version.Activate(AnnaId, Monday))
        };

        // Assert
        Assert.All(errors, e => Assert.Equal("An active version cannot be changed", e.Message));
        Assert.Equal("Access control", version.Requirements[0].Title);
    }

    [Fact]
    public void A_version_without_requirements_cannot_be_activated()
    {
        // Arrange
        var version = CreateFramework().LatestVersion;

        // Act
        var error = Assert.Throws<DomainException>(() => version.Activate(AnnaId, Monday));

        // Assert
        Assert.Equal("Add at least one requirement before activating", error.Message);
        Assert.False(version.IsActive);
    }

    [Fact]
    public void The_version_activated_last_is_current_and_the_earlier_one_is_kept()
    {
        // Arrange
        var framework = CreateFramework();
        var first = framework.LatestVersion;
        first.AddRequirement("A.5.15", "Access control", null);
        first.Activate(AnnaId, Monday);
        var second = framework.AddVersion(PdfFile(marker: 2), AnnaId, Monday.AddDays(1));
        second.AddRequirement("A.5.15", "Access control", null);

        // Act
        var beforeActivation = framework.CurrentVersion(Today);
        second.Activate(AnnaId, Monday.AddDays(2));

        // Assert
        Assert.Same(first, beforeActivation);
        Assert.Same(second, framework.CurrentVersion(Today));
        Assert.True(first.IsActive);
    }
}
