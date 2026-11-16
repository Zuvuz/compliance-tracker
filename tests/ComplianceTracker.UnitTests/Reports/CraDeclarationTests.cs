using ComplianceTracker.Application.Reports;

namespace ComplianceTracker.UnitTests.Reports;

// US-181: edge cases of the draft EU declaration of conformity (CRA Annex V). Arrange / Act / Assert.
public class CraDeclarationTests
{
    private static CraProduct Product(string? classCode = "ImportantClassI", string? version = "2.1.0", string? purpose = "Customer portal",
        IReadOnlyList<string>? applied = null)
        => new("Acme Portal", version, purpose, "Acme ApS", "12345678", "DK", classCode, applied ?? ["ISO/IEC 27001:2022"]);

    private static string Text(CraProduct product) => string.Join("\n", CraDeclaration.Build(product).Blocks.Select(b => b.Text));

    [Theory]
    [InlineData("Default", "Module A (internal control)")]
    [InlineData("ImportantClassI", "Module B+C or H, or harmonised standards with module A")]
    [InlineData("ImportantClassII", "Module B+C or H, or a European cybersecurity certification scheme")]
    [InlineData("Critical", "A European cybersecurity certificate where required under Art. 8(1)")]
    public void The_module_follows_the_class(string classCode, string expected)
        => Assert.StartsWith(expected, CraDeclaration.Module(classCode));

    [Theory]
    [InlineData(null)]
    [InlineData("NotApplicable")]
    [InlineData("SomethingElse")]
    public void Without_a_usable_class_the_procedure_is_to_be_completed(string? classCode)
    {
        Assert.Null(CraDeclaration.Module(classCode));
        Assert.Contains("Procedure: To be completed: classify the product", Text(Product(classCode)));
        Assert.Contains("Conformity assessment procedure: classify the product", CraDeclaration.Missing(Product(classCode)));
    }

    [Fact]
    public void Every_item_of_annex_v_is_there()
    {
        var headings = CraDeclaration.Build(Product()).Blocks.Where(b => b.Kind == DocumentBlockKind.Heading2).Select(b => b.Text[..2]).ToList();

        Assert.Equal(["1.", "2.", "3.", "4.", "5.", "6.", "7.", "8."], headings);
    }

    [Fact]
    public void No_product_version_is_to_be_completed()
    {
        Assert.Contains("Version: To be completed", Text(Product(version: " ")));
        Assert.Contains("Version", CraDeclaration.Missing(Product(version: null)));
    }

    [Fact]
    public void No_intended_purpose_is_to_be_completed()
        => Assert.Contains("Intended purpose", CraDeclaration.Missing(Product(purpose: "")));

    [Fact]
    public void No_standards_applied_is_to_be_completed()
    {
        Assert.Contains("Harmonised standards, common specifications or certification applied", CraDeclaration.Missing(Product(applied: [])));
        Assert.DoesNotContain("Harmonised standards, common specifications or certification applied", CraDeclaration.Missing(Product()));
    }

    [Fact]
    public void A_class_that_may_need_a_notified_body_asks_for_it()
    {
        Assert.Contains("Notified body and certificate, where one took part", CraDeclaration.Missing(Product("Critical")));
        Assert.Contains("Notified body: not applicable with module A", Text(Product("Default")));
        Assert.DoesNotContain("Notified body and certificate, where one took part", CraDeclaration.Missing(Product("Default")));
    }

    [Fact]
    public void The_draft_is_never_presented_as_signed()
    {
        var text = Text(Product("Default"));

        Assert.Contains(CraDeclaration.DraftNotice, text);
        Assert.Contains("Signature: not signed (draft)", text);
        Assert.Contains("Place and date of issue, name, function and signature", CraDeclaration.Missing(Product("Default")));
    }

    [Fact]
    public void The_address_is_always_to_be_completed_with_the_country()
        => Assert.Contains("Address: To be completed (country: DK)", Text(Product()));

    [Fact]
    public void The_check_lists_each_missing_field()
    {
        var check = CraDeclaration.CheckText(Product(version: null));

        Assert.StartsWith("CRA declaration of conformity (draft)", check);
        Assert.Contains("[ ] Version: To be completed", check);
    }
}
