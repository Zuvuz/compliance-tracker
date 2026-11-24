using ComplianceTracker.Application.Isms;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Isms;

// US-214: importing assets from a spreadsheet, the filters and order of the inventory, and the inventory as a workbook.
// Every test follows Arrange / Act / Assert.
public class AssetInventoryTests
{
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Organisation = Guid.NewGuid();

    private static IReadOnlyList<User> Members()
    {
        var acme = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        acme.AddMember(new UserIdentity("dan-sub", "Dan", "dan@acme.test"), UserRole.Developer);
        return acme.Members;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Sheet(params string[] lines)
        => lines.Select(l => (IReadOnlyList<string>)l.Split(';')).ToList();

    private const string Header = "Name;Kind;Owner;Classification;Personal data;Description";

    private static IReadOnlyList<AssetImportRow> Read(params string[] rows)
        => AssetImportRows.Read(Sheet([Header, .. rows]), Members(), ClassificationScheme.Create(Organisation), []);

    // ---------------------------------------------------------- Import

    [Fact]
    public void A_good_row_names_the_member_and_the_level()
    {
        var row = Read("Customer database;information;dan@acme.test;confidential;yes;CRM data").Single();

        Assert.True(row.IsValid);
        Assert.Equal(AssetKind.Information, row.Kind);
        Assert.Equal("Dan", row.Owner!.Name);
        Assert.Equal("Confidential", row.Classification);
        Assert.True(row.HoldsPersonalData);
        Assert.Equal("CRM data", row.Description);
    }

    [Fact]
    public void An_unknown_classification_is_an_error_on_its_row()
    {
        var row = Read("Customer database;Information;Anna;Secret;;").Single();

        Assert.Contains("Secret is not a level in the classification scheme", row.Errors);
    }

    [Fact]
    public void An_owner_who_is_not_a_member_is_an_error()
    {
        var row = Read("Customer database;Information;Zoe;Internal;;").Single();

        Assert.Contains("Zoe is not a member of the organisation", row.Errors);
    }

    [Theory]
    [InlineData("Robot")]
    [InlineData("7")]
    [InlineData("")]
    public void A_kind_that_does_not_exist_is_an_error(string kind)
    {
        var row = Read($"Webshop;{kind};Anna;Internal;;").Single();

        Assert.False(row.IsValid);
        Assert.StartsWith("The kind must be one of", row.Errors.Single());
    }

    [Fact]
    public void The_same_name_twice_in_the_file_is_an_error_on_the_second_row()
    {
        var rows = Read("Webshop;Software;Anna;Internal;;", "", "webshop;Software;Anna;Internal;;");

        Assert.True(rows[0].IsValid);
        Assert.Equal(2, rows[1].Number);
        Assert.Contains("webshop is in the file twice", rows[1].Errors);
    }

    [Fact]
    public void A_name_already_in_the_inventory_is_an_error()
    {
        var rows = AssetImportRows.Read(Sheet(Header, "Webshop;Software;Anna;Internal;;"), Members(), ClassificationScheme.Create(Organisation), ["WEBSHOP"]);

        Assert.Contains("Webshop is already in the inventory", rows.Single().Errors);
    }

    [Fact]
    public void A_file_without_the_needed_columns_says_what_it_has()
    {
        var errors = AssetImportRows.FileErrors(Sheet("Title;Owner", "Webshop;Anna"));

        Assert.Equal("The file needs the columns Name, Kind, Owner and Classification in its first row. It has: Title, Owner", errors.Single());
    }

    [Fact]
    public void A_file_with_only_the_header_has_no_assets()
    {
        Assert.Equal("The file has no assets", AssetImportRows.FileErrors(Sheet(Header, ";;;;;")).Single());
    }

    // ---------------------------------------------------------- The inventory

    private static AssetLine Line(string name, string level, string? owner, bool personal = false, AssetKind kind = AssetKind.Information)
        => new(Asset.Create(Organisation, name, kind, Guid.NewGuid(), level, personal, null, ClassificationScheme.Create(Organisation), Monday),
            owner, [], [], []);

    [Fact]
    public void Assets_without_an_owner_come_first_then_the_highest_classification()
    {
        var lines = new[] { Line("A", "Public", "Anna"), Line("B", "Strictly confidential", "Anna"), Line("C", "Internal", null) };

        var shown = AssetService.Filter(lines, new AssetFilter(), ClassificationScheme.Create(Organisation));

        Assert.Equal(["C", "B", "A"], shown.Select(l => l.Asset.Name));
    }

    [Fact]
    public void The_personal_data_filter_is_the_list_for_gdpr_article_30()
    {
        var lines = new[] { Line("CRM", "Internal", "Anna", personal: true), Line("Logo", "Public", "Anna") };

        var shown = AssetService.Filter(lines, new AssetFilter(PersonalData: true), ClassificationScheme.Create(Organisation));

        Assert.Equal("CRM", shown.Single().Asset.Name);
    }

    [Fact]
    public void The_filters_on_kind_classification_and_owner_combine()
    {
        var lines = new[]
        {
            Line("Laptop", "Internal", null, kind: AssetKind.Hardware),
            Line("Server", "Confidential", null, kind: AssetKind.Hardware),
            Line("CRM", "Internal", null)
        };

        var shown = AssetService.Filter(lines, new AssetFilter(AssetKind.Hardware, "internal", NoOwner: true), ClassificationScheme.Create(Organisation));

        Assert.Equal("Laptop", shown.Single().Asset.Name);
    }

    [Fact]
    public void The_workbook_marks_an_asset_without_an_owner()
    {
        var view = new AssetsView([Line("CRM", "Confidential", null, personal: true)], 1, ClassificationScheme.Create(Organisation), new AssetFilter(),
            new Dictionary<Guid, string>(), [], [], [], true);

        var report = AssetInventoryReport.Build(view, "Acme ApS", Monday);

        var row = report.Sheets.Single().Rows.Single();
        Assert.Equal("No owner", row.Cells[2]);
        Assert.Equal("Yes", row.Cells[4]);
        Assert.Equal(ReportStatus.Bad, row.Status);
        Assert.Contains(("Without an owner", "1"), report.Summary);
    }
}
