using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Infrastructure.Persistence;

namespace ComplianceTracker.UnitTests.Frameworks;

// Edge-case unit tests of US-142: the built-in NIS2 catalogues (R5, R6, R7, R8) and coverage across layers (R6).
// Arrange / Act / Assert.
public class Nis2CatalogueTests
{
    private static readonly IReadOnlyList<IReadOnlyList<BuiltInNis2Requirements.Item>> All =
        [BuiltInNis2Requirements.Nis2, BuiltInNis2Requirements.Danish, BuiltInNis2Requirements.Technical];

    [Fact]
    public void No_NIS2_obligation_is_missing_and_none_is_extra()
    {
        // Act
        var codes = BuiltInNis2Requirements.Nis2.Select(i => i.Code).ToList();

        // Assert
        Assert.Equal(BuiltInNis2Requirements.Nis2Codes.Order(), codes.Order());
    }

    [Fact]
    public void Each_of_the_ten_measures_of_article_21_is_its_own_requirement()
    {
        // Act
        var points = BuiltInNis2Requirements.Nis2.Where(i => i.Code.StartsWith("Art. 21(2)(", StringComparison.Ordinal)).Select(i => i.Code[^2]).ToList();

        // Assert
        Assert.Equal("abcdefghij", string.Concat(points));
    }

    [Fact]
    public void Every_Danish_and_technical_requirement_implements_a_NIS2_requirement_that_exists()
    {
        // Arrange
        var nis2 = BuiltInNis2Requirements.Nis2.Select(i => i.Code).ToHashSet();

        // Act
        var layers = BuiltInNis2Requirements.Danish.Concat(BuiltInNis2Requirements.Technical).ToList();

        // Assert
        Assert.All(layers, i => Assert.Contains(i.Implements!, nis2));
    }

    [Fact]
    public void The_Danish_law_has_each_of_the_ten_measures_in_paragraph_6()
    {
        // Act
        var implemented = BuiltInNis2Requirements.Danish.Select(i => i.Implements).ToHashSet();

        // Assert: every Art. 20, 21(2) and 23(4) obligation of NIS2 has a Danish counterpart
        Assert.All(BuiltInNis2Requirements.Nis2Codes.Where(c => c.StartsWith("Art. 20", StringComparison.Ordinal)
                || c.StartsWith("Art. 21(2)", StringComparison.Ordinal) || c.StartsWith("Art. 23(4)", StringComparison.Ordinal)),
            code => Assert.Contains(code, implemented));
    }

    [Fact]
    public void Codes_are_unique_within_each_catalogue_and_ids_across_all()
    {
        // Assert
        Assert.All(All, catalogue => Assert.Equal(catalogue.Count, catalogue.Select(i => i.Code).Distinct().Count()));
        var ids = All.SelectMany(c => c.SelectMany(i => new[] { i.Id, i.DanishTextId })).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Every_requirement_has_a_Danish_text_and_fits_the_columns()
    {
        // Assert: the column sizes of Requirements and RequirementTexts
        Assert.All(All.SelectMany(c => c), i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.DanishTitle), i.Code);
            Assert.False(string.IsNullOrWhiteSpace(i.DanishDescription), i.Code);
            Assert.True(i.Code.Length <= 50 && i.Title.Length <= 500 && i.DanishTitle.Length <= 500, i.Code);
            Assert.True(i.Description.Length <= 4000 && i.DanishDescription.Length <= 4000, i.Code);
            Assert.True((i.Group?.Length ?? 0) <= 200 && i.Reference.Length <= 500, i.Code);
            Assert.StartsWith("https://", i.Reference);
        });
    }

    [Fact]
    public void Positions_follow_the_order_of_each_catalogue()
    {
        // Assert
        Assert.All(All, catalogue => Assert.Equal(Enumerable.Range(1, catalogue.Count), catalogue.Select(i => i.Position)));
    }

    [Fact]
    public void The_Danish_law_is_a_national_layer_of_NIS2_in_Denmark()
    {
        // Assert
        Assert.Equal("DK", BuiltInFrameworks.Nis2Denmark.Country);
        Assert.Equal(new DateOnly(2025, 7, 1), BuiltInFrameworks.Nis2Denmark.AppliesFrom);
        Assert.StartsWith("NIS 2-loven", BuiltInFrameworks.Nis2Denmark.Name);
    }

    // ---------------------------------------------------------- Coverage across layers (NationalLayers)

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private static Framework Catalogue(string name, params (string Code, string? Implements)[] requirements)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Org, name, CatalogFile.Create("f.pdf", bytes), Anna, Monday, FrameworkLevel.Organisation);
        foreach (var (code, implements) in requirements)
        {
            var requirement = framework.LatestVersion.AddRequirement(code, code, null);
            if (implements is not null)
            {
                typeof(Requirement).GetProperty(nameof(Requirement.Implements))!.SetValue(requirement, implements);
            }
        }

        return framework;
    }

    private static void Implements(Framework layer, Framework directive)
        => typeof(Framework).GetProperty(nameof(Framework.ImplementsFrameworkId))!.SetValue(layer, directive.Id);

    // A row that is covered by evidence (the organisation area), or not covered
    private static RequirementCoverage Row(Requirement requirement, bool covered, string? notApplicable = null)
        => new(requirement, [], notApplicable)
        {
            EvidenceCovers = true,
            Evidence = covered ? [new EvidenceItem(Domain.Mappings.Evidence.ForLink(Guid.NewGuid(), "Policy", "https://example.com/p", null, Anna, null, Monday), false)] : []
        };

    private static FrameworkCompliance Compliance(Framework framework, params bool[] covered)
        => new(framework, framework.LatestVersion, framework.LatestVersion.Requirements.Select((r, i) => Row(r, covered[i])).ToList());

    [Fact]
    public void A_covered_directive_requirement_covers_the_national_one()
    {
        // Arrange
        var nis2 = Catalogue("NIS2 – Directive", ("Art. 21(2)(b)", null));
        var danish = Catalogue("NIS 2-loven – DK", ("§ 6, stk. 1, nr. 2", "Art. 21(2)(b)"));
        Implements(danish, nis2);

        // Act
        var result = NationalLayers.Apply([Compliance(nis2, true), Compliance(danish, false)]);

        // Assert
        var row = result[1].Requirements.Single();
        Assert.True(row.IsCovered);
        Assert.False(row.IsOwnCovered);
        Assert.Equal("NIS2 Art. 21(2)(b)", row.CoveredThrough);
    }

    [Fact]
    public void A_covered_national_requirement_covers_the_directive_one()
    {
        // Arrange
        var nis2 = Catalogue("NIS2 – Directive", ("Art. 21(2)(b)", null));
        var danish = Catalogue("NIS 2-loven – DK", ("§ 6, stk. 1, nr. 2", "Art. 21(2)(b)"));
        Implements(danish, nis2);

        // Act
        var result = NationalLayers.Apply([Compliance(nis2, false), Compliance(danish, true)]);

        // Assert
        Assert.Equal("NIS 2-loven § 6, stk. 1, nr. 2", result[0].Requirements.Single().CoveredThrough);
        Assert.Equal(1, result[0].Covered);
    }

    [Fact]
    public void Nothing_is_passed_on_when_neither_side_is_covered_or_both_are()
    {
        // Arrange
        var nis2 = Catalogue("NIS2", ("A", null), ("B", null));
        var danish = Catalogue("DK", ("§ 1", "A"), ("§ 2", "B"));
        Implements(danish, nis2);

        // Act
        var result = NationalLayers.Apply([Compliance(nis2, false, true), Compliance(danish, false, true)]);

        // Assert
        Assert.All(result.SelectMany(f => f.Requirements), r => Assert.Null(r.CoveredThrough));
        Assert.Equal(1, result[0].Covered);
    }

    [Fact]
    public void Without_the_directive_a_national_layer_stays_as_it_is()
    {
        // Arrange
        var nis2 = Catalogue("NIS2", ("A", null));
        var danish = Catalogue("DK", ("§ 1", "A"));
        Implements(danish, nis2);

        // Act: only the Danish law is followed
        var result = NationalLayers.Apply([Compliance(danish, false)]);

        // Assert
        Assert.False(result.Single().Requirements.Single().IsCovered);
    }

    [Fact]
    public void A_not_applicable_requirement_neither_gives_nor_gets_coverage()
    {
        // Arrange
        var nis2 = Catalogue("NIS2", ("A", null));
        var danish = Catalogue("DK", ("§ 1", "A"));
        Implements(danish, nis2);
        var directive = new FrameworkCompliance(nis2, nis2.LatestVersion, [Row(nis2.LatestVersion.Requirements[0], true, notApplicable: "Not our sector")]);

        // Act
        var result = NationalLayers.Apply([directive, Compliance(danish, false)]);

        // Assert
        Assert.Null(result[1].Requirements.Single().CoveredThrough);
    }

    [Fact]
    public void Two_national_requirements_for_the_same_article_both_get_coverage()
    {
        // Arrange: § 12 and § 15, stk. 1 both implement Art. 23(1)
        var nis2 = Catalogue("NIS2", ("Art. 23(1)", null));
        var danish = Catalogue("DK", ("§ 12", "Art. 23(1)"), ("§ 15, stk. 1", "Art. 23(1)"));
        Implements(danish, nis2);

        // Act
        var result = NationalLayers.Apply([Compliance(nis2, true), Compliance(danish, false, false)]);

        // Assert
        Assert.Equal(2, result[1].Covered);
    }

    [Fact]
    public void Evidence_covers_only_in_the_organisation_area()
    {
        // Arrange
        var nis2 = Catalogue("NIS2", ("A", null));
        var withEvidence = Row(nis2.LatestVersion.Requirements[0], true);

        // Act
        var inAProject = withEvidence with { EvidenceCovers = false };

        // Assert
        Assert.True(withEvidence.IsCovered);
        Assert.False(inAProject.IsCovered);
    }
}
