using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Tests.Frameworks;

// Unit tests of CatalogFile.Create (US-005 R5, R6): file type, size limit, safe file name and fingerprint.
// Every test follows Arrange / Act / Assert.
public class CatalogFileTests
{
    // A "PDF" of the given size: the PDF signature followed by zeros
    private static byte[] Pdf(long size = 100)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return bytes;
    }

    // ---------------------------------------------------------- Valid file

    [Fact]
    public void Accepts_a_pdf_and_keeps_its_content()
    {
        // Arrange
        var content = Pdf();

        // Act
        var file = CatalogFile.Create("policy.pdf", content);

        // Assert
        Assert.Equal("policy.pdf", file.FileName);
        Assert.Equal(content, file.Content);
    }

    // ---------------------------------------------------------- Size (R5, ASVS V5.2.1)

    [Fact]
    public void Accepts_a_pdf_of_exactly_20_MB()
    {
        // Arrange
        var content = Pdf(CatalogFile.MaxSizeInBytes);

        // Act
        var error = Record.Exception(() => CatalogFile.Create("policy.pdf", content));

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Rejects_a_pdf_one_byte_over_20_MB()
    {
        // Arrange
        var content = Pdf(CatalogFile.MaxSizeInBytes + 1);

        // Act
        var error = Record.Exception(() => CatalogFile.Create("policy.pdf", content));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("The file must be 20 MB or smaller", domainError.Message);
    }

    // The size is checked first, so a huge file is rejected before its content is looked at
    [Fact]
    public void Reports_the_size_before_the_file_type()
    {
        // Arrange
        var hugeTextFile = new byte[CatalogFile.MaxSizeInBytes + 1];

        // Act
        var error = Record.Exception(() => CatalogFile.Create("notes.txt", hugeTextFile));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("The file must be 20 MB or smaller", domainError.Message);
    }

    // ---------------------------------------------------------- File type (R5, ASVS V5.2.2)

    [Theory]
    [InlineData("policy.pdf")]
    [InlineData("policy.PDF")]  // upper case extension
    [InlineData("policy.Pdf")]  // mixed case extension
    public void Accepts_the_pdf_extension_in_any_case(string fileName)
    {
        // Arrange
        var content = Pdf();

        // Act
        var error = Record.Exception(() => CatalogFile.Create(fileName, content));

        // Assert
        Assert.Null(error);
    }

    [Theory]
    [InlineData("policy.txt")]      // another extension
    [InlineData("policy")]          // no extension
    [InlineData("policy.pdf.exe")]  // .pdf is not the last extension
    [InlineData("policypdf")]       // "pdf" without the dot
    public void Rejects_a_pdf_with_the_wrong_extension(string fileName)
    {
        // Arrange: real PDF content, only the name is wrong
        var content = Pdf();

        // Act
        var error = Record.Exception(() => CatalogFile.Create(fileName, content));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Only PDF files are allowed", domainError.Message);
    }

    [Theory]
    [InlineData("This is not a PDF")]  // a text file
    [InlineData("%PDF")]               // the signature without the dash
    [InlineData("%pdf-1.7")]           // the signature in lower case
    [InlineData(" %PDF-1.7")]          // the signature, but not at the very start
    [InlineData("")]                   // an empty file
    public void Rejects_content_that_is_not_a_pdf(string text)
    {
        // Arrange: a .pdf name, but the content is not a PDF
        var content = Encoding.ASCII.GetBytes(text);

        // Act
        var error = Record.Exception(() => CatalogFile.Create("policy.pdf", content));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Only PDF files are allowed", domainError.Message);
    }

    // ---------------------------------------------------------- File name (R6, ASVS V5.3.2)

    [Theory]
    [InlineData("../../policy.pdf", "policy.pdf")]            // path traversal
    [InlineData("/etc/policy.pdf", "policy.pdf")]             // absolute Linux path
    [InlineData(@"C:\Users\anna\policy.pdf", "policy.pdf")]   // Windows path
    [InlineData(@"..\..\policy.pdf", "policy.pdf")]           // Windows path traversal
    [InlineData("  policy.pdf", "policy.pdf")]                // spaces before the name are removed
    [InlineData("my policy.pdf", "my policy.pdf")]            // spaces inside the name are kept
    public void Keeps_only_the_file_name_without_folders(string uploadedName, string expectedName)
    {
        // Arrange
        var content = Pdf();

        // Act
        var file = CatalogFile.Create(uploadedName, content);

        // Assert
        Assert.Equal(expectedName, file.FileName);
    }

    // The name must end with ".pdf"; spaces after it make it another extension (Windows does not allow such names either)
    [Fact]
    public void Rejects_a_name_with_spaces_after_the_extension()
    {
        // Arrange
        var content = Pdf();

        // Act
        var error = Record.Exception(() => CatalogFile.Create("policy.pdf  ", content));

        // Assert
        var domainError = Assert.IsType<DomainException>(error);
        Assert.Equal("Only PDF files are allowed", domainError.Message);
    }

    // ---------------------------------------------------------- Fingerprint (SHA-256)

    [Fact]
    public void Fingerprint_is_the_SHA256_of_the_content_in_lower_case_hex()
    {
        // Arrange
        var content = Pdf();
        var expected = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        // Act
        var file = CatalogFile.Create("policy.pdf", content);

        // Assert
        Assert.Equal(expected, file.Sha256);
        Assert.Matches("^[0-9a-f]{64}$", file.Sha256);
    }

    // The fingerprint depends only on the content, so an auditor can recalculate it from the PDF
    [Fact]
    public void The_same_content_gets_the_same_fingerprint_whatever_the_name()
    {
        // Arrange
        var content = Pdf();

        // Act
        var first = CatalogFile.Create("first.pdf", content);
        var second = CatalogFile.Create("second.pdf", content);

        // Assert
        Assert.Equal(first.Sha256, second.Sha256);
    }

    [Fact]
    public void Different_content_gets_a_different_fingerprint()
    {
        // Arrange
        var original = Pdf();
        var changed = Pdf();
        changed[^1] = 1; // change the last byte

        // Act
        var first = CatalogFile.Create("policy.pdf", original);
        var second = CatalogFile.Create("policy.pdf", changed);

        // Assert
        Assert.NotEqual(first.Sha256, second.Sha256);
    }
}
