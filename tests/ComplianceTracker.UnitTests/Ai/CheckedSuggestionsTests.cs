using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.Ai;

// US-205 R9, R11, R13: the cross-mapping check, evidence through its stories, and the evaluation. Arrange / Act / Assert.
public class CheckedSuggestionsTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Framework Catalogue(string name, params string[] codes)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Org, name, CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        foreach (var code in codes)
        {
            framework.LatestVersion.AddRequirement(code, $"Title {code}", null);
        }

        return framework;
    }

    private static RequirementMapping Mapping(double score = 0.9)
    {
        var (a, b) = (Catalogue("A", "A-1"), Catalogue("B", "B-1"));
        return RequirementMapping.Suggest(Org, a.LatestVersion.Id, b.LatestVersion.Id, a.LatestVersion.Requirements[0].Id, b.LatestVersion.Requirements[0].Id,
            score, 1, "why", "embed", Monday);
    }

    // ---------------------------------------------------------- The cross-mapping check (R13)

    [Theory]
    [InlineData("VERDICT: Same\nDIFFERS: Nothing", MappingStrength.Same, null)]
    [InlineData("VERDICT: Partly.\nDIFFERS: Only one asks for a log", MappingStrength.Partly, "Only one asks for a log")]
    [InlineData("**VERDICT:** Related\nDIFFERS: Different topics", MappingStrength.Related, "Different topics")]
    [InlineData("VERDICT: Different", MappingStrength.Related, null)]
    public void The_mapping_check_is_understood(string answer, MappingStrength strength, string? differs)
    {
        var (parsed, says) = MappingCheckPrompt.Parse(answer);

        Assert.Equal(strength, parsed);
        Assert.Equal(differs, says is null || says.Equals("nothing", StringComparison.OrdinalIgnoreCase) ? null : says);
    }

    [Theory]
    [InlineData("")]
    [InlineData("They look alike to me")]
    [InlineData("VERDICT: perhaps")]
    public void A_mapping_answer_without_a_verdict_is_refused(string answer)
        => Assert.Throws<AiUnavailableException>(() => MappingCheckPrompt.Parse(answer));

    [Fact]
    public void Only_a_checked_Same_carries_coverage()
    {
        var unchecked_ = Mapping();
        unchecked_.Approve(Anna, null, Monday);
        var isChecked = Mapping();
        isChecked.Checked(MappingStrength.Same, "Nothing", "qwen3:14b");
        isChecked.Approve(Anna, null, Monday);

        Assert.Equal((false, true, null), (unchecked_.IsApprovedSame, isChecked.IsApprovedSame, isChecked.Differs));
    }

    [Fact]
    public void The_check_sets_the_strength_even_against_the_score()
    {
        var mapping = Mapping(0.9);

        mapping.Checked(MappingStrength.Partly, "One is about logs", "qwen3:14b");

        Assert.Equal((MappingStrength.Partly, "One is about logs", "qwen3:14b", true), (mapping.Strength, mapping.Differs, mapping.CheckModel, mapping.IsChecked));
    }

    [Fact]
    public void A_check_names_its_model()
        => Assert.Throws<DomainException>(() => Mapping().Checked(MappingStrength.Same, null, " "));

    // ---------------------------------------------------------- Through the stories (R11)

    private static (FrameworkCompliance Framework, WorkItem Story, Requirement Linked, Requirement Other) Covered(bool needsReview = false)
    {
        var cra = Catalogue("CRA – Regulation", "I.2(d)", "I.2(e)");
        var (linked, other) = (cra.LatestVersion.Requirements[0], cra.LatestVersion.Requirements[1]);
        var story = WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem("#12", "https://github.com/x/y/issues/12", "[US-012] Lock account", "", "", "", "user-story", false), Monday);
        var link = RequirementLink.Create(Project, linked.Id, story.Id, Anna, Monday);
        var rows = new List<RequirementCoverage>
        {
            new(linked, [new LinkedWorkItem(needsReview ? link.CarryTo(linked.Id, textChanged: true) : link, story)]),
            new(other, [])
        };
        return (new FrameworkCompliance(cra, cra.LatestVersion, rows), story, linked, other);
    }

    [Fact]
    public void Evidence_reaches_the_requirements_of_its_stories()
    {
        var (framework, story, linked, _) = Covered();

        var through = EvidenceThrough.For([story], [framework], new HashSet<Guid>());

        Assert.Equal([("[US-012] Lock account", linked.Id, "CRA I.2(d)")], through.Select(t => (t.Story.Title, t.Requirement.Id, t.Label)));
    }

    [Fact]
    public void A_requirement_the_evidence_is_linked_to_directly_is_not_shown_again()
    {
        var (framework, story, linked, _) = Covered();

        Assert.Empty(EvidenceThrough.For([story], [framework], new HashSet<Guid> { linked.Id }));
    }

    [Fact]
    public void A_link_that_waits_for_review_reaches_nothing()
    {
        var (framework, story, _, _) = Covered(needsReview: true);

        Assert.Empty(EvidenceThrough.For([story], [framework], new HashSet<Guid>()));
    }

    // ---------------------------------------------------------- The evaluation (R9)

    private static DocumentLinkSuggestion Decided(bool approved, string? checkedBy, Guid? document = null, string code = "I.2(d)", Guid? framework = null)
    {
        var suggestion = DocumentLinkSuggestion.Create(Org, document ?? Guid.NewGuid(), framework ?? Guid.NewGuid(), code, 0.8, 1, "embed", "v", Monday);
        if (checkedBy is not null)
        {
            suggestion.Checked(new PassageCheck(PassageVerdict.Meets, "a passage", null, checkedBy));
        }

        if (approved)
        {
            suggestion.Approve(Anna, Monday);
        }
        else
        {
            suggestion.Reject(RejectionReason.WrongRequirement, null, Anna, Monday);
        }

        return suggestion;
    }

    [Fact]
    public void The_evaluation_compares_checked_with_not_checked_and_counts_what_was_found()
    {
        DocumentLinkSuggestion[] decided =
        [
            Decided(true, "qwen3:14b"), Decided(true, "qwen3:14b"), Decided(false, "qwen3:14b"), Decided(true, "qwen2.5:3b"),
            Decided(true, null), Decided(false, null), Decided(false, null), Decided(false, null)
        ];

        var result = PassageCheckEvaluation.Measure(decided, linkedByHand: 2);

        Assert.Equal("qwen3:14b", result.Model);
        Assert.Equal(0.75, result.CheckedRight);
        Assert.Equal(0.25, result.NotCheckedRight);
        Assert.Equal(4.0 / 6, result.Found!.Value, 6);
    }

    [Fact]
    public void Nothing_of_a_kind_gives_no_share()
    {
        var result = PassageCheckEvaluation.Measure([Decided(false, null)], 0);

        // No right answer is known (nothing approved, nothing linked by hand), so nothing can be found either
        Assert.Equal(("not checked", (double?)null, 0.0, (double?)null), (result.Model, result.CheckedRight, result.NotCheckedRight!.Value, result.Found));
        Assert.Null(PassageCheckEvaluation.Measure([], 0).Found);
    }
}
