using ComplianceTracker.Domain.ActivityLog;

namespace ComplianceTracker.Domain.Tests.ActivityLog;

// Unit tests of ActivityLogEntry: a log line stores exactly what it was given (ISO 27001 A.8.15).
// Every test follows Arrange / Act / Assert.
public class ActivityLogEntryTests
{
    [Fact]
    public void Stores_who_did_what_where_and_when()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var entry = new ActivityLogEntry(organizationId, userId, ActivityAction.OrganizationCreated, occurredAt);

        // Assert
        Assert.Equal(organizationId, entry.OrganizationId);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(ActivityAction.OrganizationCreated, entry.Action);
        Assert.Equal(occurredAt, entry.OccurredAt);
    }

    // Actions such as signing in are not about anything in particular
    [Fact]
    public void Has_no_subject_unless_one_is_given()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var entry = new ActivityLogEntry(organizationId, userId, ActivityAction.UserSignedIn, DateTime.UtcNow);

        // Assert
        Assert.Null(entry.SubjectId);
    }

    // E.g. the framework version for CatalogImported, or the project for ProjectCreated
    [Theory]
    [InlineData(ActivityAction.CatalogImported)]
    [InlineData(ActivityAction.ProjectCreated)]
    public void Stores_what_the_entry_is_about(ActivityAction action)
    {
        // Arrange
        var subjectId = Guid.NewGuid();

        // Act
        var entry = new ActivityLogEntry(Guid.NewGuid(), Guid.NewGuid(), action, DateTime.UtcNow, subjectId);

        // Assert
        Assert.Equal(subjectId, entry.SubjectId);
    }
}
