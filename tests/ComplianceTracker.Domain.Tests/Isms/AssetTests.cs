using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-214: the assets and the organisation's classification scheme. Every test follows Arrange / Act / Assert.
public class AssetTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Bo = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);

    private static ClassificationScheme Scheme() => ClassificationScheme.Create(Organisation);

    private static Asset Database(ClassificationScheme? scheme = null, Guid? owner = null)
        => Asset.Create(Organisation, "Customer database", AssetKind.Information, owner ?? Anna, "Confidential", true, null, scheme ?? Scheme(), Monday);

    // ---------------------------------------------------------- R1: name, kind and owner

    [Fact]
    public void An_asset_has_a_name_a_kind_an_owner_and_a_classification()
    {
        var asset = Database();

        Assert.Equal("Customer database", asset.Name);
        Assert.Equal(AssetKind.Information, asset.Kind);
        Assert.Equal(Anna, asset.OwnerId);
        Assert.Equal("Confidential", asset.Classification);
        Assert.True(asset.HoldsPersonalData);
        Assert.True(asset.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_asset_needs_a_name(string? name)
    {
        var error = Assert.Throws<DomainException>(() => Asset.Create(Organisation, name, AssetKind.Software, Anna, "Internal", false, null, Scheme(), Monday));

        Assert.Equal("Give the asset a name", error.Message);
    }

    [Fact]
    public void A_name_longer_than_the_limit_is_refused()
    {
        var name = new string('a', Asset.MaxNameLength + 1);

        Assert.Throws<DomainException>(() => Asset.Create(Organisation, name, AssetKind.Software, Anna, "Internal", false, null, Scheme(), Monday));
    }

    [Fact]
    public void The_name_is_trimmed()
    {
        var asset = Asset.Create(Organisation, "  Webshop  ", AssetKind.Software, Anna, "Internal", false, "  ", Scheme(), Monday);

        Assert.Equal("Webshop", asset.Name);
        Assert.Null(asset.Description);
    }

    [Fact]
    public void An_asset_needs_a_kind_that_exists()
    {
        Assert.Throws<DomainException>(() => Asset.Create(Organisation, "Webshop", null, Anna, "Internal", false, null, Scheme(), Monday));
        Assert.Throws<DomainException>(() => Asset.Create(Organisation, "Webshop", (AssetKind)42, Anna, "Internal", false, null, Scheme(), Monday));
    }

    [Fact]
    public void An_asset_needs_an_owner()
    {
        var error = Assert.Throws<DomainException>(() => Asset.Create(Organisation, "Webshop", AssetKind.Software, Guid.Empty, "Internal", false, null, Scheme(), Monday));

        Assert.Equal("Choose the owner of the asset", error.Message);
    }

    // ---------------------------------------------------------- R2: the classification

    [Fact]
    public void The_classification_is_written_as_the_scheme_writes_it()
    {
        var asset = Asset.Create(Organisation, "Webshop", AssetKind.Software, Anna, "  strictly CONFIDENTIAL ", false, null, Scheme(), Monday);

        Assert.Equal("Strictly confidential", asset.Classification);
    }

    [Fact]
    public void A_classification_not_in_the_scheme_is_refused()
    {
        var error = Assert.Throws<DomainException>(() => Asset.Create(Organisation, "Webshop", AssetKind.Software, Anna, "Secret", false, null, Scheme(), Monday));

        Assert.Equal("Secret is not a level in the classification scheme", error.Message);
    }

    [Fact]
    public void A_missing_classification_asks_for_one()
    {
        var error = Assert.Throws<DomainException>(() => Asset.Create(Organisation, "Webshop", AssetKind.Software, Anna, null, false, null, Scheme(), Monday));

        Assert.Equal("Choose the classification of the asset", error.Message);
    }

    [Fact]
    public void The_scheme_starts_with_four_levels_lowest_first()
    {
        var scheme = Scheme();

        Assert.Equal(["Public", "Internal", "Confidential", "Strictly confidential"], scheme.Levels);
    }

    [Fact]
    public void A_level_in_use_cannot_be_removed_from_the_scheme()
    {
        var scheme = Scheme();

        var error = Assert.Throws<DomainException>(() => scheme.Change(["Public", "Internal"], ["Confidential", "Internal"]));

        Assert.Equal("Confidential is used by assets; give them another level first", error.Message);
        Assert.Equal(4, scheme.Levels.Count);
    }

    [Fact]
    public void Levels_not_in_use_can_be_removed_and_new_ones_added()
    {
        var scheme = Scheme();

        var changed = scheme.Change(["Public", " Internal ", "", "Secret"], ["internal"]);

        Assert.True(changed);
        Assert.Equal(["Public", "Internal", "Secret"], scheme.Levels);
    }

    [Fact]
    public void The_same_levels_again_change_nothing()
    {
        var scheme = Scheme();

        Assert.False(scheme.Change(ClassificationScheme.DefaultLevels, []));
    }

    [Fact]
    public void A_scheme_needs_a_level_and_no_level_twice()
    {
        var scheme = Scheme();

        Assert.Throws<DomainException>(() => scheme.Change(["", " "], []));
        var twice = Assert.Throws<DomainException>(() => scheme.Change(["Public", "public"], []));
        Assert.Equal("Public is in the scheme twice", twice.Message);
    }

    [Fact]
    public void A_scheme_has_at_most_ten_levels()
    {
        var scheme = Scheme();

        Assert.Throws<DomainException>(() => scheme.Change(Enumerable.Range(1, ClassificationScheme.MaxLevels + 1).Select(i => $"Level {i}"), []));
    }

    // ---------------------------------------------------------- R3: an owner who left

    [Fact]
    public void An_asset_whose_owner_is_no_longer_a_member_has_no_owner()
    {
        var asset = Database(owner: Bo);

        Assert.True(asset.HasOwner([Anna, Bo]));
        Assert.False(asset.HasOwner([Anna]));
    }

    // ---------------------------------------------------------- R4: the highest classification

    [Fact]
    public void The_highest_classification_follows_the_order_of_the_scheme()
    {
        var scheme = Scheme();

        Assert.Equal("Confidential", scheme.Highest(["Internal", "confidential", "Public"]));
    }

    [Fact]
    public void Without_known_levels_there_is_no_highest()
    {
        var scheme = Scheme();

        Assert.Null(scheme.Highest([]));
        Assert.Null(scheme.Highest(["Secret", null]));
    }

    // ---------------------------------------------------------- Links and changes

    [Fact]
    public void An_asset_can_be_in_two_projects()
    {
        var asset = Database();
        var webshop = Guid.NewGuid();
        var app = Guid.NewGuid();

        var changed = asset.Link([webshop, app, webshop, Guid.Empty], null, null);

        Assert.True(changed);
        Assert.Equal([webshop, app], asset.ProjectIds);
    }

    [Fact]
    public void The_same_links_in_another_order_change_nothing()
    {
        var asset = Database();
        var webshop = Guid.NewGuid();
        var app = Guid.NewGuid();
        asset.Link([webshop, app], null, null);

        Assert.False(asset.Link([app, webshop], [], []));
    }

    [Fact]
    public void A_change_names_the_fields_that_changed()
    {
        var scheme = Scheme();
        var asset = Database(scheme);

        var changed = asset.Change("Customer database", AssetKind.Service, Bo, "Strictly confidential", true, null, scheme);

        Assert.Equal(["kind", "owner", "classification"], changed);
    }

    [Fact]
    public void A_removed_asset_cannot_be_changed_or_removed_again()
    {
        var scheme = Scheme();
        var asset = Database(scheme);
        asset.Remove(Monday);

        Assert.False(asset.IsActive);
        Assert.Throws<DomainException>(() => asset.Remove(Monday));
        Assert.Throws<DomainException>(() => asset.Change("X", AssetKind.Service, Anna, "Public", false, null, scheme));
        Assert.Throws<DomainException>(() => asset.Link([], [], []));
    }
}
