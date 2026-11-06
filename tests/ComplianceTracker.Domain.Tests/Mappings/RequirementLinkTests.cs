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

    // ---------------------------------------------------------- US-110: review after a version change

    [Fact]
    public void Carrying_to_an_unchanged_requirement_needs_no_review()
    {
        // Arrange
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday);

        // Act
        var carried = link.CarryTo(Guid.NewGuid());

        // Assert
        Assert.False(carried.NeedsReview);
        Assert.Equal(link.RequirementId, carried.CarriedFromRequirementId);
    }

    [Fact]
    public void Carrying_to_a_changed_requirement_needs_review()
    {
        // Arrange
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday);

        // Act
        var carried = link.CarryTo(Guid.NewGuid(), textChanged: true);

        // Assert
        Assert.True(carried.NeedsReview);
        Assert.Equal(link.LinkedById, carried.LinkedById);
    }

    [Fact]
    public void A_link_still_waiting_keeps_waiting_against_its_first_text()
    {
        // Arrange
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday);
        var waiting = link.CarryTo(Guid.NewGuid(), textChanged: true);

        // Act
        var again = waiting.CarryTo(Guid.NewGuid());

        // Assert
        Assert.True(again.NeedsReview);
        Assert.Equal(link.RequirementId, again.CarriedFromRequirementId);
    }

    [Fact]
    public void Marking_reviewed_keeps_who_and_when()
    {
        // Arrange
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday)
            .CarryTo(Guid.NewGuid(), textChanged: true);
        var bo = Guid.NewGuid();

        // Act
        link.MarkReviewed(bo, Monday.AddDays(1));

        // Assert
        Assert.False(link.NeedsReview);
        Assert.Equal(bo, link.ReviewedById);
        Assert.Equal(Monday.AddDays(1), link.ReviewedAt);
    }

    [Fact]
    public void A_link_that_needs_no_review_cannot_be_marked_reviewed()
    {
        // Arrange
        var link = RequirementLink.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Monday);

        // Act
        var error = Assert.Throws<DomainException>(() => link.MarkReviewed(Guid.NewGuid(), Monday));

        // Assert
        Assert.Equal("Nothing to review", error.Message);
    }
}
