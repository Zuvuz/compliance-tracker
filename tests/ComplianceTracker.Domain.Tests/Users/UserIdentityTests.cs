using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Users;

// Unit tests of UserIdentity, the signed-in person's identity (US-004).
// Every test follows Arrange / Act / Assert.
public class UserIdentityTests
{
    // A record: two identities with the same values are equal, which keeps comparisons simple
    [Fact]
    public void Identities_with_the_same_values_are_equal()
    {
        // Arrange
        var first = new UserIdentity("anna-id", "Anna", "anna@example.com");
        var second = new UserIdentity("anna-id", "Anna", "anna@example.com");

        // Act
        var areEqual = first == second;

        // Assert
        Assert.True(areEqual);
    }

    // Two people can share a name and an e-mail; the sign-in id tells them apart (ASVS V10.5.2)
    [Fact]
    public void Identities_with_different_ids_are_not_equal()
    {
        // Arrange
        var anna = new UserIdentity("anna-id", "Anna", "anna@example.com");
        var otherAnna = new UserIdentity("other-id", "Anna", "anna@example.com");

        // Act
        var areEqual = anna == otherAnna;

        // Assert
        Assert.False(areEqual);
    }
}
