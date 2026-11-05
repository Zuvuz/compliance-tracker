using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using System.Text;

namespace ComplianceTracker.Domain.Tests.Frameworks;

// Unit tests of Framework and FrameworkVersion (US-005): the first version, new versions and the current version.
// The rule that names are unique per organisation needs the database and is tested in the Specs project.
// Every test follows Arrange / Act / Assert.
public class FrameworkTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    // A valid PDF file. The marker makes the content (and so the fingerprint) different for each file.
    private static CatalogFile PdfFile(string fileName = "policy.pdf", byte marker = 0)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        bytes[^1] = marker;
        return CatalogFile.Create(fileName, bytes);
    }

    private static Framework CreateFramework(string name = "ISO/IEC 27001", CatalogFile? file = null)
        => Framework.Create(OrganizationId, name, file ?? PdfFile(), AnnaId, Monday);

    // ---------------------------------------------------------- Create

    [Fact]
    public void Stores_the_owner_and_the_name()
    {
        // Arrange
        const string name = "ISO/IEC 27001";

        // Act
        var framework = CreateFramework(name);

        // Assert
        Assert.Equal(OrganizationId, framework.OwnerOrganizationId);
        Assert.Equal(name, framework.Name);
    }

    [Fact]
    public void Removes_spaces_around_the_name()
    {
        // Arrange
        const string name = "  Security policy  ";

        // Act
        var framework = CreateFramework(name);

        // Assert
        Assert.Equal("Security policy", framework.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_an_empty_name(string name)
    {
        // Arrange: the name comes from InlineData

        // Act
        var error = Record.Exception(() => CreateFramework(name));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Framework name is required", domainError.Message);
    }

    // A framework never exists without a version
    [Fact]
    public void Starts_with_the_uploaded_file_as_version_1()
    {
        // Arrange
        var file = PdfFile("iso27001.pdf");

        // Act
        var framework = CreateFramework(file: file);

        // Assert
        var version = Assert.Single(framework.Versions);
        Assert.Equal(1, version.Number);
        Assert.Same(version, framework.LatestVersion);
    }

    [Fact]
    public void The_first_version_stores_the_file_details_and_who_uploaded_it()
    {
        // Arrange
        var file = PdfFile("iso27001.pdf");

        // Act
        var framework = CreateFramework(file: file);

        // Assert
        var version = framework.LatestVersion;
        Assert.Equal(framework.Id, version.FrameworkId);
        Assert.Equal("iso27001.pdf", version.FileName);
        Assert.Equal(file.Content.Length, version.SizeInBytes);
        Assert.Equal(file.Sha256, version.Sha256);
        Assert.Equal(AnnaId, version.UploadedById);
        Assert.Equal(Monday, version.UploadedAt);
    }

    // ---------------------------------------------------------- New versions (R3)

    [Fact]
    public void A_new_upload_gets_the_next_version_number()
    {
        // Arrange
        var framework = CreateFramework();

        // Act
        var second = framework.AddVersion(PdfFile(marker: 2), AnnaId, Monday.AddDays(1));
        var third = framework.AddVersion(PdfFile(marker: 3), AnnaId, Monday.AddDays(2));

        // Assert
        Assert.Equal(2, second.Number);
        Assert.Equal(3, third.Number);
        Assert.Equal(new[] { 1, 2, 3 }, framework.Versions.Select(v => v.Number));
    }

    [Fact]
    public void The_newest_upload_is_the_latest_version()
    {
        // Arrange
        var framework = CreateFramework();

        // Act
        var newest = framework.AddVersion(PdfFile("policy-v2.pdf", marker: 2), AnnaId, Monday.AddDays(1));

        // Assert
        Assert.Same(newest, framework.LatestVersion);
    }

    [Fact]
    public void Earlier_versions_are_kept_unchanged()
    {
        // Arrange
        var framework = CreateFramework(file: PdfFile("policy-v1.pdf", marker: 1));
        var first = framework.LatestVersion;
        var firstFingerprint = first.Sha256;

        // Act
        framework.AddVersion(PdfFile("policy-v2.pdf", marker: 2), AnnaId, Monday.AddDays(1));

        // Assert
        Assert.Contains(first, framework.Versions);
        Assert.Equal(1, first.Number);
        Assert.Equal("policy-v1.pdf", first.FileName);
        Assert.Equal(firstFingerprint, first.Sha256);
    }

    [Fact]
    public void Each_version_gets_its_own_id()
    {
        // Arrange
        var framework = CreateFramework();

        // Act
        var second = framework.AddVersion(PdfFile(marker: 2), AnnaId, Monday.AddDays(1));

        // Assert
        Assert.NotEqual(framework.Versions[0].Id, second.Id);
    }

    // ---------------------------------------------------------- Built-in frameworks (US-006)

    [Fact]
    public void An_uploaded_framework_is_not_built_in()
    {
        // Arrange (the helper creates an uploaded framework)

        // Act
        var framework = CreateFramework();

        // Assert
        Assert.False(framework.IsBuiltIn);
    }

    [Fact]
    public void A_built_in_framework_cannot_get_a_new_version()
    {
        // Arrange: built-in frameworks only come from the migration's seed data, so the test
        // removes the owner the same way EF Core loads a seeded row (no owner = built-in)
        var framework = CreateFramework();
        typeof(Framework).GetProperty(nameof(Framework.OwnerOrganizationId))!.SetValue(framework, null);

        // Act
        var exception = Record.Exception(() => framework.AddVersion(PdfFile(marker: 1), AnnaId, Monday));

        // Assert
        Assert.True(framework.IsBuiltIn);
        Assert.IsType<DomainException>(exception);
        Assert.Single(framework.Versions);
    }
}
