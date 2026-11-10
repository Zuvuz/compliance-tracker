using System.IO.Compression;
using System.Text;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Domain.Tests.Frameworks;

// Edge-case unit tests of US-143 in the domain: the requirement tree, kinds, and the file formats. Arrange / Act / Assert.
public class RequirementTreeTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private static byte[] PdfBytes()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return bytes;
    }

    private static FrameworkVersion Draft()
        => Framework.Create(OrganizationId, "ISO/IEC 27001", CatalogFile.Create("iso.pdf", PdfBytes()), AnnaId, Monday).LatestVersion;

    private static byte[] Package(params string[] parts)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var part in parts)
            {
                using var writer = new StreamWriter(zip.CreateEntry(part).Open());
                writer.Write("<x/>");
            }
        }

        return stream.ToArray();
    }

    // ---------------------------------------------------------- R4: the tree

    [Fact]
    public void A_requirement_is_placed_under_its_parent_by_code()
    {
        // Arrange
        var version = Draft();
        var parent = version.AddRequirement("6.1.2", "Risk assessment", null);

        // Act
        var child = version.AddRequirement("6.1.2 a)", "Criteria", null, RequirementKind.Requirement, " 6.1.2 ");

        // Assert
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal([child], version.ChildrenOf(parent.Id));
    }

    [Fact]
    public void An_unknown_parent_puts_it_at_the_top()
    {
        // Act
        var child = Draft().AddRequirement("6.1.2 a)", "Criteria", null, RequirementKind.Requirement, "9.9");

        // Assert
        Assert.Null(child.ParentId);
    }

    [Fact]
    public void A_parent_listed_after_its_child_is_placed_afterwards()
    {
        // Arrange
        var version = Draft();
        var child = version.AddRequirement("A.5.1", "Policies", null);
        var parent = version.AddRequirement("A.5", "Organizational", null);

        // Act
        version.PlaceUnder("A.5.1", "A.5");

        // Assert
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public void A_requirement_is_never_placed_under_itself_or_its_own_descendant()
    {
        // Arrange
        var version = Draft();
        var top = version.AddRequirement("6", "Planning", null);
        var middle = version.AddRequirement("6.1", "Actions", null, RequirementKind.Requirement, "6");
        version.AddRequirement("6.1.1", "General", null, RequirementKind.Requirement, "6.1");

        // Act
        version.PlaceUnder("6", "6.1.1");
        version.PlaceUnder("6.1", "6.1");

        // Assert
        Assert.Null(top.ParentId);
        Assert.Equal(top.Id, middle.ParentId);
    }

    [Fact]
    public void Removing_a_parent_moves_what_was_under_it_up()
    {
        // Arrange
        var version = Draft();
        var top = version.AddRequirement("6", "Planning", null);
        var middle = version.AddRequirement("6.1", "Actions", null, RequirementKind.Requirement, "6");
        var bottom = version.AddRequirement("6.1.1", "General", null, RequirementKind.Requirement, "6.1");

        // Act
        version.RemoveRequirement(middle.Id);

        // Assert
        Assert.Equal(top.Id, bottom.ParentId);
    }

    [Fact]
    public void Clearing_a_draft_removes_every_requirement_but_an_active_version_cannot_be_cleared()
    {
        // Arrange
        var version = Draft();
        version.AddRequirement("5.1", "Policies", null);

        // Act
        version.ClearRequirements();
        version.AddRequirement("5.2", "Roles", null);
        version.Activate(AnnaId, Monday);

        // Assert
        Assert.Equal(["5.2"], version.Requirements.Select(r => r.Code));
        Assert.Throws<DomainException>(version.ClearRequirements);
        Assert.Throws<DomainException>(() => version.PlaceUnder("5.2", "5.2"));
    }

    // ---------------------------------------------------------- R5: kinds

    [Fact]
    public void A_requirement_is_a_requirement_unless_told_otherwise()
    {
        // Act
        var requirement = Draft().AddRequirement("5.1", "Policies", null);

        // Assert
        Assert.Equal(RequirementKind.Requirement, requirement.Kind);
        Assert.False(requirement.IsRecommendation);
    }

    [Fact]
    public void The_kind_can_be_changed_in_a_draft_only()
    {
        // Arrange
        var version = Draft();
        var requirement = version.AddRequirement("5.1", "Policies", null);

        // Act
        version.ChangeKind(requirement.Id, RequirementKind.Recommendation);
        version.Activate(AnnaId, Monday);

        // Assert
        Assert.True(requirement.IsRecommendation);
        Assert.Throws<DomainException>(() => version.ChangeKind(requirement.Id, RequirementKind.Requirement));
    }

    [Fact]
    public void An_unknown_kind_is_refused()
    {
        // Arrange
        var version = Draft();

        // Act / Assert
        Assert.Equal("Choose Requirement, Recommendation or Characteristic",
            Assert.Throws<DomainException>(() => version.AddRequirement("5.1", "Policies", null, (RequirementKind)7)).Message);
    }

    // ---------------------------------------------------------- R2: the formats

    [Theory]
    [InlineData("iso.docx", CatalogFormat.Word)]
    [InlineData("ISO.DOCX", CatalogFormat.Word)]
    public void A_word_file_is_a_zip_package_with_a_document(string name, CatalogFormat expected)
    {
        // Act
        var file = CatalogFile.Create(name, Package("[Content_Types].xml", "word/document.xml"));

        // Assert
        Assert.Equal(expected, file.Format);
    }

    [Fact]
    public void An_excel_file_is_a_zip_package_with_a_workbook()
    {
        // Act / Assert
        Assert.Equal(CatalogFormat.Excel, CatalogFile.Create("soa.xlsx", Package("xl/workbook.xml")).Format);
    }

    [Theory]
    [InlineData("soa.xlsx")]  // a Word package named .xlsx
    [InlineData("iso.docx")]  // a zip without a document
    public void A_package_with_the_wrong_inside_is_refused(string name)
    {
        // Act / Assert
        Assert.Equal(CatalogFile.WrongType, Assert.Throws<DomainException>(() => CatalogFile.Create(name, Package("other/part.xml"))).Message);
    }

    [Fact]
    public void Broken_zip_bytes_are_refused()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => CatalogFile.Create("iso.docx", [0x50, 0x4B, 0x03, 0x04, 1, 2, 3]));
    }

    [Theory]
    [InlineData("soa.csv", "Control;Name\nA.5.1;Policies", CatalogFormat.Csv)]
    [InlineData("page.html", "<!DOCTYPE html><html><body>5.1</body></html>", CatalogFormat.Html)]
    [InlineData("page.htm", "<HTML><body>5.1</body></HTML>", CatalogFormat.Html)]
    public void Text_formats_are_accepted(string name, string content, CatalogFormat expected)
    {
        // Act / Assert
        Assert.Equal(expected, CatalogFile.Create(name, Encoding.UTF8.GetBytes(content)).Format);
    }

    [Theory]
    [InlineData("soa.csv", "")]                       // empty
    [InlineData("soa.csv", "a\0b")]                   // binary
    [InlineData("page.html", "just text, no html")]   // not a page
    [InlineData("soa.csv", "%PDF-1.7")]               // a PDF renamed to .csv
    [InlineData("iso.doc", "old Word")]               // old formats are not read
    [InlineData("iso.xls", "old Excel")]
    public void Wrong_content_or_an_unknown_type_is_refused(string name, string content)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => CatalogFile.Create(name, Encoding.UTF8.GetBytes(content)));
    }

    [Fact]
    public void An_html_file_is_never_sent_as_a_page()
    {
        // Act / Assert
        Assert.Equal("text/plain", CatalogFile.ContentTypeOf(CatalogFormat.Html));
        Assert.Equal("application/pdf", CatalogFile.ContentTypeOf(CatalogFormat.Pdf));
    }

    [Fact]
    public void Remembered_columns_are_kept_trimmed()
    {
        // Arrange
        var framework = Framework.Create(OrganizationId, "ISO/IEC 27001", CatalogFile.Create("iso.pdf", PdfBytes()), AnnaId, Monday);

        // Act
        framework.RememberColumns("  a  ");

        // Assert
        Assert.Equal("a", framework.TableColumns);
    }
}
