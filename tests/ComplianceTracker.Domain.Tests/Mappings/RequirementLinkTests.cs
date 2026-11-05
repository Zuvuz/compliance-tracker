using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Unit tests of RequirementLink (US-021). The rules that need the database (the requirement is in the
// project's framework version, the story is the project's, no duplicate) are tested in the Specs project.
// Every test follows Arrange / Act / Assert.
public class RequirementLinkTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_new_link_is_active_and_made_by_hand()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), userId, Monday);

        // Assert
        Assert.True(link.IsActive);
        Assert.Equal(LinkSource.Manual, link.Source);
        Assert.Equal(userId, link.LinkedById);
        Assert.Equal(Monday, link.LinkedAt);
    }

    [Fact]
    public void Removing_keeps_the_link_with_who_and_when()
    {
        // Arrange
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday);
        var bo = Guid.NewGuid();

        // Act
        link.Remove(bo, Monday.AddDays(1));

        // Assert
        Assert.False(link.IsActive);
        Assert.Equal(bo, link.RemovedById);
        Assert.Equal(Monday.AddDays(1), link.RemovedAt);
    }

    [Fact]
    public void A_link_cannot_be_removed_twice()
    {
        // Arrange
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday);
        link.Remove(Guid.NewGuid(), Monday);

        // Act
        var error = Assert.Throws<DomainException>(() => link.Remove(Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("The link is already removed", error.Message);
    }
}
