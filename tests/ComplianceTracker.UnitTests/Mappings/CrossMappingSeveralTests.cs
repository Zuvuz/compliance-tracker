using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Mappings;

// Edge-case unit tests of US-206: several frameworks in one run, and matches limited to chosen projects. Arrange / Act / Assert.
public class CrossMappingSeveralTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private static Framework Catalogue(string name, params string[] codes)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Org, name, CatalogFile.Create("f.pdf", bytes), Anna, Monday, FrameworkLevel.Organisation);
        foreach (var code in codes)
        {
            framework.LatestVersion.AddRequirement(code, $"Title {code}", null);
        }

        return framework;
    }

    private static FollowedVersion Followed(Framework framework) => new(framework, framework.LatestVersion);

    private static RequirementMapping Approved(Framework from, Framework to)
    {
        var mapping = RequirementMapping.Suggest(Org, from.LatestVersion.Id, to.LatestVersion.Id, from.LatestVersion.Requirements[0].Id,
            to.LatestVersion.Requirements[0].Id, 0.9, 1, "why", "fake-embed", Monday);
        mapping.Checked(MappingStrength.Same, null, "fake-chat"); // US-205 R13
        mapping.Approve(Anna, null, Monday);
        return mapping;
    }

    private static FrameworkCompliance Compliance(Framework framework, bool covered)
        => new(framework, framework.LatestVersion, framework.LatestVersion.Requirements.Select(r => new RequirementCoverage(r, [])
        {
            EvidenceCovers = true,
            Evidence = covered ? [new EvidenceItem(Evidence.ForLink(Guid.NewGuid(), "Policy", "https://example.com/p", null, Anna, null, Monday), false)] : []
        }).ToList());

    // ---------------------------------------------------------- Several at once (R1)

    [Fact]
    public void Every_pair_comes_once_with_the_earlier_chosen_on_the_left()
        => Assert.Equal([("A", "B"), ("A", "C"), ("B", "C")], CrossMappingService.Pairs(["A", "B", "C"]));

    [Fact]
    public void Two_frameworks_are_one_pair_and_one_is_none()
    {
        Assert.Equal([("A", "B")], CrossMappingService.Pairs(["A", "B"]));
        Assert.Empty(CrossMappingService.Pairs(["A"]));
    }

    [Fact]
    public void The_frameworks_are_chosen_in_the_order_given_and_each_once()
    {
        var a = Followed(Catalogue("A", "A-1"));
        var b = Followed(Catalogue("B", "B-1"));
        var c = Followed(Catalogue("C", "C-1"));

        var chosen = CrossMappingService.Choose([a, b, c], [c.Version.Id, a.Version.Id, c.Version.Id]);

        Assert.Equal([c, a], chosen);
    }

    [Fact]
    public void One_framework_or_the_same_one_twice_is_not_enough()
    {
        var a = Followed(Catalogue("A", "A-1"));

        Assert.Equal("Choose two different frameworks", Assert.Throws<DomainException>(() => CrossMappingService.Choose([a], [a.Version.Id, a.Version.Id])).Message);
    }

    [Fact]
    public void A_framework_the_organisation_does_not_follow_is_refused()
    {
        var a = Followed(Catalogue("A", "A-1"));

        Assert.Equal("Choose a framework the organisation follows",
            Assert.Throws<DomainException>(() => CrossMappingService.Choose([a], [a.Version.Id, Guid.NewGuid()])).Message);
    }

    [Fact]
    public void More_than_five_at_once_is_refused()
    {
        var followed = Enumerable.Range(1, 6).Select(i => Followed(Catalogue($"F{i}", $"R-{i}"))).ToList();

        Assert.Equal("Choose at most 5 frameworks at once",
            Assert.Throws<DomainException>(() => CrossMappingService.Choose(followed, followed.Select(v => v.Version.Id).ToList())).Message);
    }

    [Theory]
    [InlineData(new[] { "CRA v2" }, "CRA v2")]
    [InlineData(new[] { "CRA v2", "NIS2 v1" }, "CRA v2 and NIS2 v1")]
    [InlineData(new[] { "CRA v2", "NIS2 v1", "ISO v1" }, "CRA v2, NIS2 v1 and ISO v1")]
    public void The_names_are_joined_like_a_sentence(string[] names, string expected)
        => Assert.Equal(expected, CrossMappingService.Names(names));

    [Fact]
    public void A_pair_is_the_same_whichever_side_it_was_suggested_from()
    {
        var a = Catalogue("A", "A-1");
        var b = Catalogue("B", "B-1");
        var mapping = Approved(a, b);

        Assert.True(CrossMappingService.SamePair(mapping, a.LatestVersion.Id, b.LatestVersion.Id));
        Assert.True(CrossMappingService.SamePair(mapping, b.LatestVersion.Id, a.LatestVersion.Id));
        Assert.False(CrossMappingService.SamePair(mapping, a.LatestVersion.Id, Guid.NewGuid()));
    }

    // ---------------------------------------------------------- Chosen projects (R3, R4)

    [Fact]
    public void An_approved_match_applies_to_every_project_until_it_is_limited()
    {
        var mapping = Approved(Catalogue("A", "A-1"), Catalogue("B", "B-1"));

        Assert.True(mapping.AppliesTo(Guid.NewGuid()));
    }

    [Fact]
    public void A_limited_match_applies_only_to_the_chosen_projects()
    {
        var portal = Guid.NewGuid();
        var mapping = Approved(Catalogue("A", "A-1"), Catalogue("B", "B-1"));

        var changed = mapping.LimitTo([portal, portal, Guid.Empty]);

        Assert.True(changed);
        Assert.Equal([portal], mapping.ProjectIds);
        Assert.True(mapping.AppliesTo(portal));
        Assert.False(mapping.AppliesTo(Guid.NewGuid()));
    }

    [Fact]
    public void Choosing_no_project_opens_the_match_to_all_again()
    {
        var mapping = Approved(Catalogue("A", "A-1"), Catalogue("B", "B-1"));
        mapping.LimitTo([Guid.NewGuid()]);

        mapping.LimitTo([]);

        Assert.Empty(mapping.ProjectIds);
        Assert.True(mapping.AppliesTo(Guid.NewGuid()));
    }

    [Fact]
    public void The_same_projects_in_another_order_change_nothing()
    {
        var (one, two) = (Guid.NewGuid(), Guid.NewGuid());
        var mapping = Approved(Catalogue("A", "A-1"), Catalogue("B", "B-1"));
        mapping.LimitTo([one, two]);

        Assert.False(mapping.LimitTo([two, one]));
    }

    [Fact]
    public void A_pending_or_rejected_match_cannot_be_limited()
    {
        var a = Catalogue("A", "A-1");
        var b = Catalogue("B", "B-1");
        var pending = RequirementMapping.Suggest(Org, a.LatestVersion.Id, b.LatestVersion.Id, a.LatestVersion.Requirements[0].Id,
            b.LatestVersion.Requirements[0].Id, 0.9, 1, "why", "m", Monday);

        Assert.Equal("Only an approved match applies to projects", Assert.Throws<DomainException>(() => pending.LimitTo([Guid.NewGuid()])).Message);
    }

    [Fact]
    public void Coverage_passes_only_in_the_projects_the_match_applies_to()
    {
        // Arrange
        var (portal, app) = (Guid.NewGuid(), Guid.NewGuid());
        var cra = Catalogue("CRA", "I.2(d)");
        var policy = Catalogue("Security policy", "SP-1");
        var mapping = Approved(policy, cra);
        mapping.LimitTo([app]);

        // Act
        var inPortal = CrossMappings.Apply([Compliance(cra, true), Compliance(policy, false)], [mapping], portal);
        var inApp = CrossMappings.Apply([Compliance(cra, true), Compliance(policy, false)], [mapping], app);

        // Assert
        Assert.Null(inPortal[1].Requirements.Single().CoveredThrough);
        Assert.Equal("CRA I.2(d)", inApp[1].Requirements.Single().CoveredThrough);
    }

    [Fact]
    public void Three_frameworks_never_pass_coverage_in_a_circle()
    {
        // Arrange: A ↔ B, B ↔ C, C ↔ A, and nothing is covered on its own
        var a = Catalogue("A", "A-1");
        var b = Catalogue("B", "B-1");
        var c = Catalogue("C", "C-1");

        // Act
        var result = CrossMappings.Apply([Compliance(a, false), Compliance(b, false), Compliance(c, false)],
            [Approved(a, b), Approved(b, c), Approved(c, a)], Guid.NewGuid());

        // Assert
        Assert.All(result.SelectMany(f => f.Requirements), r => Assert.False(r.IsCovered));
    }
}
