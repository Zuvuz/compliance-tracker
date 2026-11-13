using ComplianceTracker.Infrastructure.Persistence;

namespace ComplianceTracker.UnitTests.Frameworks;

// US-152: edge-case unit tests of US-062 (the AI Act's requirements), US-068 (coverage list) and US-069 (the CRA's
// obligations beyond Annex I). The seed data must be sound: a duplicate id or code breaks a migration or a mapping.
// Arrange / Act / Assert.
public class BuiltInCatalogueTests
{
    private static readonly (string Name, IReadOnlyList<(Guid Id, int Position, string Group, string Code, string Title, string Description)> Items)[] Catalogues =
    [
        ("CRA", BuiltInCraRequirements.All.Select(i => (i.Id, i.Position, i.Group, i.Code, i.Title, i.Description)).ToList()),
        ("CRA v2", BuiltInCraRequirementsV2.All.Select(i => (i.Id, i.Position, i.Group, i.Code, i.Title, i.Description)).ToList()),
        ("AI Act", BuiltInAiActRequirements.All.Select(i => (i.Id, i.Position, i.Group, i.Code, i.Title, i.Description)).ToList())
    ];

    public static TheoryData<string> Names => new(Catalogues.Select(c => c.Name));

    private static IReadOnlyList<(Guid Id, int Position, string Group, string Code, string Title, string Description)> Items(string name)
        => Catalogues.Single(c => c.Name == name).Items;

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_requirement_has_its_own_id_code_and_position(string name)
    {
        var items = Items(name);

        Assert.NotEmpty(items);
        Assert.DoesNotContain(items, i => i.Id == Guid.Empty);
        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
        Assert.Equal(items.Count, items.Select(i => i.Code).Distinct().Count());
        Assert.Equal(items.Count, items.Select(i => i.Position).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_requirement_has_a_group_a_title_and_a_text_without_stray_spaces(string name)
    {
        Assert.All(Items(name), i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.Group));
            Assert.False(string.IsNullOrWhiteSpace(i.Title));
            Assert.False(string.IsNullOrWhiteSpace(i.Description));
            Assert.Equal(i.Code.Trim(), i.Code);
            Assert.Equal(i.Title.Trim(), i.Title);
        });
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_positions_are_in_the_order_the_requirements_are_listed(string name)
    {
        var positions = Items(name).Select(i => i.Position).ToList();

        Assert.Equal(positions.Order(), positions);
    }

    [Fact]
    public void No_id_is_used_by_two_catalogues()
    {
        var ids = Catalogues.SelectMany(c => c.Items.Select(i => i.Id)).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Version_2_of_the_CRA_keeps_every_Annex_I_requirement_of_version_1()
    {
        var v2 = BuiltInCraRequirementsV2.All.Select(i => i.Code).ToHashSet();

        Assert.All(BuiltInCraRequirements.All, i => Assert.Contains(i.Code, v2));
    }

    [Fact]
    public void Every_reference_points_at_the_official_text_on_EUR_Lex()
    {
        Assert.All(BuiltInCraRequirementsV2.All, i => Assert.StartsWith("https://eur-lex.europa.eu/", i.Reference));
        Assert.All(BuiltInAiActRequirements.All, i => Assert.StartsWith("https://eur-lex.europa.eu/", i.Reference));
    }
}
