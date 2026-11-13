using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Mappings;

// Edge-case unit tests of US-037: the strength, the best matches, the decisions and coverage through an approved "Same".
// Arrange / Act / Assert.
public class CrossMappingTests
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

    private static RequirementMapping Mapping(Framework from, Framework to, double score, int fromIndex = 0, int toIndex = 0)
        => RequirementMapping.Suggest(Org, from.LatestVersion.Id, to.LatestVersion.Id, from.LatestVersion.Requirements[fromIndex].Id,
            to.LatestVersion.Requirements[toIndex].Id, score, 1, "why", "fake-embed", Monday);

    private static RequirementCoverage Row(Requirement requirement, bool covered, string? notApplicable = null)
        => new(requirement, [], notApplicable)
        {
            EvidenceCovers = true,
            Evidence = covered ? [new EvidenceItem(Evidence.ForLink(Guid.NewGuid(), "Policy", "https://example.com/p", null, Anna, null, Monday), false)] : []
        };

    private static FrameworkCompliance Compliance(Framework framework, bool covered, string? notApplicable = null)
        => new(framework, framework.LatestVersion, framework.LatestVersion.Requirements.Select(r => Row(r, covered, notApplicable)).ToList());

    // ---------------------------------------------------------- Strength (R2)

    [Theory]
    [InlineData(0.85, MappingStrength.Same)]
    [InlineData(0.8499, MappingStrength.Partly)]
    [InlineData(0.7, MappingStrength.Partly)]
    [InlineData(0.6999, MappingStrength.Related)]
    [InlineData(0, MappingStrength.Related)]
    public void The_strength_follows_the_score(double score, MappingStrength expected)
        => Assert.Equal(expected, RequirementMapping.StrengthOf(score));

    [Theory]
    [InlineData(1.7, 1.0)]
    [InlineData(-0.3, 0.0)]
    [InlineData(double.NaN, 0.0)]
    [InlineData(0.912345, 0.9123)]
    public void A_score_out_of_range_is_kept_within_0_to_1(double score, double expected)
    {
        var mapping = Mapping(Catalogue("A", "A-1"), Catalogue("B", "B-1"), score);

        Assert.Equal(expected, mapping.Score);
    }

    [Fact]
    public void A_version_cannot_be_matched_with_itself_and_the_rank_starts_at_1()
    {
        var a = Catalogue("A", "A-1", "A-2");
        var requirements = a.LatestVersion.Requirements;

        Assert.Throws<DomainException>(() => RequirementMapping.Suggest(Org, a.LatestVersion.Id, a.LatestVersion.Id, requirements[0].Id, requirements[1].Id, 0.9, 1, "", "m", Monday));
        Assert.Throws<DomainException>(() => RequirementMapping.Suggest(Org, a.LatestVersion.Id, Guid.NewGuid(), requirements[0].Id, requirements[1].Id, 0.9, 0, "", "m", Monday));
    }

    [Fact]
    public void A_long_rationale_is_cut_to_500_characters()
    {
        var a = Catalogue("A", "A-1");
        var mapping = RequirementMapping.Suggest(Org, a.LatestVersion.Id, Guid.NewGuid(), a.LatestVersion.Requirements[0].Id, Guid.NewGuid(), 0.9, 1, new string('x', 900), "m", Monday);

        Assert.Equal(500, mapping.Rationale.Length);
    }

    // ---------------------------------------------------------- The best matches (R2, R4)

    [Fact]
    public void Each_requirement_gets_at_most_4_matches_over_the_limit_best_first()
    {
        // Arrange: one requirement on the left, six on the right, scored 0.9, 0.8 … 0.4
        var left = Catalogue("A", "A-1").LatestVersion.Requirements;
        var right = Catalogue("B", "B-1", "B-2", "B-3", "B-4", "B-5", "B-6").LatestVersion.Requirements;
        var scores = new[] { 0.6, 0.9, 0.4, 0.8, 0.7, 0.5 };
        var vectors = new List<float[]> { new float[] { 1, 0 } };
        vectors.AddRange(scores.Select(s => new[] { (float)s, (float)Math.Sqrt(1 - s * s) }));

        // Act
        var best = CrossMappingService.Best(left, right, vectors, 0.55, (_, _) => false);

        // Assert
        Assert.Equal(["B-2", "B-4", "B-5", "B-1"], best.Select(b => b.To.Code));
        Assert.Equal([1, 2, 3, 4], best.Select(b => b.Rank));
    }

    [Fact]
    public void A_decided_pair_is_left_out_and_the_next_one_moves_up()
    {
        var left = Catalogue("A", "A-1").LatestVersion.Requirements;
        var right = Catalogue("B", "B-1", "B-2").LatestVersion.Requirements;
        var vectors = new List<float[]> { new float[] { 1, 0 }, new float[] { 1, 0 }, new[] { 0.8f, 0.6f } };

        var best = CrossMappingService.Best(left, right, vectors, 0.5, (a, b) => b == right[0].Id);

        Assert.Equal(("B-2", 1), (best.Single().To.Code, best.Single().Rank));
    }

    [Fact]
    public void A_mapping_joins_its_pair_both_ways_round()
    {
        var a = Catalogue("A", "A-1");
        var b = Catalogue("B", "B-1");
        var mapping = Mapping(a, b, 0.9);

        Assert.True(mapping.Joins(a.LatestVersion.Requirements[0].Id, b.LatestVersion.Requirements[0].Id));
        Assert.True(mapping.Joins(b.LatestVersion.Requirements[0].Id, a.LatestVersion.Requirements[0].Id));
        Assert.False(mapping.Joins(a.LatestVersion.Requirements[0].Id, Guid.NewGuid()));
    }

    [Fact]
    public void The_rationale_names_the_strength_both_requirements_and_the_score()
    {
        var a = Catalogue("A", "A-1").LatestVersion.Requirements[0];
        var b = Catalogue("B", "B-1").LatestVersion.Requirements[0];

        Assert.Equal("Same: \"A-1 Title A-1\" and \"B-1 Title B-1\" are worded alike (similarity 0.91)", CrossMappingService.Rationale(a, b, 0.912));
    }

    // ---------------------------------------------------------- Decisions (R3)

    [Fact]
    public void Rejecting_needs_a_reason_and_Other_needs_a_comment()
    {
        var mapping = Mapping(Catalogue("A", "A-1"), Catalogue("B", "B-1"), 0.9);

        Assert.Equal("Choose a reason", Assert.Throws<DomainException>(() => mapping.Reject(null, null, Anna, null, Monday)).Message);
        Assert.Equal("Explain the reason in a comment", Assert.Throws<DomainException>(() => mapping.Reject(RejectionReason.Other, " ", Anna, null, Monday)).Message);
        Assert.Throws<DomainException>(() => mapping.Reject((RejectionReason)99, null, Anna, null, Monday));
        Assert.True(mapping.IsPending);
    }

    [Fact]
    public void A_match_is_decided_once_and_asking_again_only_replaces_pending_ones()
    {
        var mapping = Mapping(Catalogue("A", "A-1"), Catalogue("B", "B-1"), 0.9);
        mapping.Approve(Anna, null, Monday);

        mapping.Replace();

        Assert.Equal(SuggestionStatus.Approved, mapping.Status);
        Assert.True(mapping.IsApprovedSame);
        Assert.Throws<DomainException>(() => mapping.Reject(RejectionReason.NotRelevant, null, Anna, null, Monday));
    }

    // ---------------------------------------------------------- Coverage through a match (R5)

    [Fact]
    public void An_approved_Same_covers_the_uncovered_side_through_the_covered_one()
    {
        // Arrange
        var cra = Catalogue("CRA – Regulation", "I.2(d)");
        var policy = Catalogue("Security policy", "SP-1");
        var mapping = Mapping(policy, cra, 0.9);
        mapping.Approve(Anna, null, Monday);

        // Act
        var result = CrossMappings.Apply([Compliance(cra, true), Compliance(policy, false)], [mapping]);

        // Assert
        var row = result[1].Requirements.Single();
        Assert.Equal("CRA I.2(d)", row.CoveredThrough);
        Assert.True(row.IsCovered);
        Assert.False(row.IsOwnCovered);
        Assert.Null(result[0].Requirements.Single().CoveredThrough);
    }

    [Theory]
    [InlineData(0.75, true)]    // Partly does not count
    [InlineData(0.9, false)]    // still pending
    public void A_Partly_or_pending_match_passes_nothing_on(double score, bool approve)
    {
        var cra = Catalogue("CRA", "I.2(d)");
        var policy = Catalogue("Security policy", "SP-1");
        var mapping = Mapping(policy, cra, score);
        if (approve)
        {
            mapping.Approve(Anna, null, Monday);
        }

        var result = CrossMappings.Apply([Compliance(cra, true), Compliance(policy, false)], [mapping]);

        Assert.Null(result[1].Requirements.Single().CoveredThrough);
    }

    [Fact]
    public void Coverage_is_not_passed_from_a_requirement_that_is_itself_only_covered_through_another()
    {
        // Arrange: C covers B through a match; B must not pass that on to A
        var a = Catalogue("A", "A-1");
        var b = Catalogue("B", "B-1");
        var c = Catalogue("C", "C-1");
        var ab = Mapping(a, b, 0.9);
        var bc = Mapping(b, c, 0.9);
        ab.Approve(Anna, null, Monday);
        bc.Approve(Anna, null, Monday);

        // Act
        var result = CrossMappings.Apply([Compliance(a, false), Compliance(b, false), Compliance(c, true)], [ab, bc]);

        // Assert
        Assert.Equal("C C-1", result[1].Requirements.Single().CoveredThrough);
        Assert.Null(result[0].Requirements.Single().CoveredThrough);
    }

    [Fact]
    public void A_match_to_a_requirement_that_does_not_apply_or_is_not_followed_passes_nothing_on()
    {
        var cra = Catalogue("CRA", "I.2(d)");
        var policy = Catalogue("Security policy", "SP-1");
        var other = Catalogue("Other", "O-1");
        var notApplicable = Mapping(policy, cra, 0.9);
        var notFollowed = Mapping(policy, other, 0.9);
        notApplicable.Approve(Anna, null, Monday);
        notFollowed.Approve(Anna, null, Monday);

        var result = CrossMappings.Apply([Compliance(cra, true, notApplicable: "Not our product"), Compliance(policy, false)], [notApplicable, notFollowed]);

        Assert.Null(result[1].Requirements.Single().CoveredThrough);
    }
}
