using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Domain.Tests.Frameworks;

// Unit tests of OrganizationFramework (US-006): switching a built-in framework on and off.
// Every test follows Arrange / Act / Assert.
public class OrganizationFrameworkTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BoId = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tuesday = Monday.AddDays(1);

    private static OrganizationFramework Enabled()
        => OrganizationFramework.Enable(OrganizationId, VersionId, AnnaId, Monday);

    [Fact]
    public void Enable_stores_who_switched_what_on_and_when()
    {
        // Arrange (the fields above)

        // Act
        var selection = Enabled();

        // Assert
        Assert.Equal(OrganizationId, selection.OrganizationId);
        Assert.Equal(VersionId, selection.FrameworkVersionId);
        Assert.Equal(AnnaId, selection.EnabledById);
        Assert.Equal(Monday, selection.EnabledAt);
    }

    [Fact]
    public void A_new_selection_is_active()
    {
        // Arrange
        var selection = Enabled();

        // Act
        var active = selection.IsActive;

        // Assert
        Assert.True(active);
        Assert.Null(selection.DisabledAt);
        Assert.Null(selection.DisabledById);
    }

    [Fact]
    public void Disable_stores_who_switched_it_off_and_when()
    {
        // Arrange
        var selection = Enabled();

        // Act
        selection.Disable(BoId, Tuesday);

        // Assert
        Assert.False(selection.IsActive);
        Assert.Equal(BoId, selection.DisabledById);
        Assert.Equal(Tuesday, selection.DisabledAt);
    }

    [Fact]
    public void Disable_keeps_the_history_of_who_switched_it_on()
    {
        // Arrange
        var selection = Enabled();

        // Act
        selection.Disable(BoId, Tuesday);

        // Assert
        Assert.Equal(AnnaId, selection.EnabledById);
        Assert.Equal(Monday, selection.EnabledAt);
    }

    [Fact]
    public void Cannot_be_switched_off_twice()
    {
        // Arrange
        var selection = Enabled();
        selection.Disable(BoId, Tuesday);

        // Act
        var exception = Record.Exception(() => selection.Disable(AnnaId, Tuesday.AddDays(1)));

        // Assert
        Assert.IsType<DomainException>(exception);
        Assert.Equal(BoId, selection.DisabledById); // the first switch-off is unchanged
    }
}
