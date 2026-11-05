using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of ProjectClassification (US-026). Every test follows Arrange / Act / Assert.
public class ProjectClassificationTests
{
    private static readonly DateTime At = new(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_classification_keeps_the_class_reason_and_who()
    {
        // Arrange
        var anna = Guid.NewGuid();

        // Act
        var classification = ProjectClassification.Create(Guid.NewGuid(), Guid.NewGuid(), "ImportantClassI",
            "  Annex III, class I, point 2  ", anna, UserRole.ComplianceManager, At);

        // Assert
        Assert.Equal("ImportantClassI", classification.Class);
        Assert.Equal("Annex III, class I, point 2", classification.Reason);
        Assert.Equal((anna, UserRole.ComplianceManager, At), (classification.ClassifiedById, classification.ClassifiedRole!.Value, classification.ClassifiedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_reason_is_required(string? reason)
    {
        // Arrange / Act / Assert
        var error = Assert.Throws<DomainException>(() => ProjectClassification.Create(Guid.NewGuid(), Guid.NewGuid(), "Critical",
            reason, Guid.NewGuid(), null, At));
        Assert.Equal("A reason is required", error.Message);
    }

    [Fact]
    public void A_class_is_required()
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => ProjectClassification.Create(Guid.NewGuid(), Guid.NewGuid(), " ", "Why",
            Guid.NewGuid(), null, At));
    }
}
