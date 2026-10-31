using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Organizations;

// Unit tests of Organization.Create (US-001, US-004): the field rules and the first Admin.
// The rules that need the database (unique CVR, one organisation per user) are tested in the Specs project.
// Every test follows Arrange / Act / Assert.
public class OrganizationTests
{
    private static readonly UserIdentity Anna = new("anna-id", "Anna", "anna@example.com");

    // A valid organisation; each test changes only the value it is about
    private static Organization Create(
        string name = "Acme ApS",
        string cvr = "12345678",
        Sector? sector = Sector.Manufacturing,
        OrganizationSize? size = OrganizationSize.Small,
        UserIdentity? creator = null)
        => Organization.Create(name, cvr, sector, size, creator ?? Anna);

    // ---------------------------------------------------------- Valid values

    [Fact]
    public void Stores_the_given_values()
    {
        // Arrange
        const string name = "Acme ApS";
        const string cvr = "12345678";

        // Act
        var organization = Create(name: name, cvr: cvr, sector: Sector.Health, size: OrganizationSize.Medium);

        // Assert
        Assert.Equal(name, organization.Name);
        Assert.Equal(cvr, organization.Cvr);
        Assert.Equal(Sector.Health, organization.Sector);
        Assert.Equal(OrganizationSize.Medium, organization.Size);
    }

    [Fact]
    public void Each_organisation_gets_its_own_id()
    {
        // Arrange
        var first = Create();

        // Act
        var second = Create();

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void The_creator_becomes_the_only_user_and_is_Admin()
    {
        // Arrange
        var creator = Anna;

        // Act
        var organization = Create(creator: creator);

        // Assert
        var user = Assert.Single(organization.Users);
        Assert.Equal(creator.ExternalId, user.ExternalId);
        Assert.Equal(creator.Name, user.Name);
        Assert.Equal(creator.Email, user.Email);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    // Every size is allowed; there is no "missing" size
    [Theory]
    [InlineData(OrganizationSize.Micro)]
    [InlineData(OrganizationSize.Small)]
    [InlineData(OrganizationSize.Medium)]
    [InlineData(OrganizationSize.Large)]
    public void Accepts_every_size(OrganizationSize size)
    {
        // Arrange: the size comes from InlineData

        // Act
        var organization = Create(size: size);

        // Assert
        Assert.Equal(size, organization.Size);
    }

    // ---------------------------------------------------------- Name (US-001 R1)

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Rejects_an_empty_name(string name)
    {
        // Arrange: the name comes from InlineData

        // Act
        var error = Record.Exception(() => Create(name: name));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Name is required", domainError.Message);
    }

    // ---------------------------------------------------------- CVR (US-001 R2)

    [Theory]
    [InlineData("12345678")]
    [InlineData("00000000")]
    [InlineData("99999999")]
    public void Accepts_exactly_eight_digits(string cvr)
    {
        // Arrange: the CVR comes from InlineData

        // Act
        var organization = Create(cvr: cvr);

        // Assert
        Assert.Equal(cvr, organization.Cvr);
    }

    [Theory]
    [InlineData("")]           // empty
    [InlineData("1234567")]    // 7 digits
    [InlineData("123456789")]  // 9 digits
    [InlineData("1234ABCD")]   // letters
    [InlineData("1234 678")]   // a space
    [InlineData(" 12345678")]  // 8 digits, but with a leading space (9 characters)
    [InlineData("1234-567")]   // other characters
    [InlineData("١٢٣٤٥٦٧٨")]   // 8 digits from another alphabet (Arabic-Indic), not 0-9
    public void Rejects_anything_but_eight_digits(string cvr)
    {
        // Arrange: the CVR comes from InlineData

        // Act
        var error = Record.Exception(() => Create(cvr: cvr));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("CVR must be 8 digits", domainError.Message);
    }

    // ---------------------------------------------------------- Sector (US-001 R3)

    [Fact]
    public void Rejects_a_missing_sector()
    {
        // Arrange
        Sector? noSector = null;

        // Act
        var error = Record.Exception(() => Create(sector: noSector));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Sector is required", domainError.Message);
    }

    [Fact]
    public void Rejects_a_missing_size()
    {
        // Arrange (US-016 R1)
        OrganizationSize? noSize = null;

        // Act
        var error = Record.Exception(() => Create(size: noSize));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Size is required", domainError.Message);
    }

    // ---------------------------------------------------------- Creator (US-004 R3)

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_a_creator_without_a_sign_in_id(string externalId)
    {
        // Arrange
        var creator = new UserIdentity(externalId, "Anna", "anna@example.com");

        // Act
        var error = Record.Exception(() => Create(creator: creator));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Creator must be signed in", domainError.Message);
    }

    // ---------------------------------------------------------- Order of the rules

    // When several rules are broken, the first one is reported, so the message is predictable
    [Fact]
    public void Reports_the_name_before_the_CVR()
    {
        // Arrange
        const string emptyName = "";
        const string invalidCvr = "123";

        // Act
        var error = Record.Exception(() => Create(name: emptyName, cvr: invalidCvr));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Name is required", domainError.Message);
    }
}
