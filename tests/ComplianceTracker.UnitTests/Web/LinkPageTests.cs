using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// Edge-case unit tests of US-220: the Link page's three columns, its marks and its filters. Arrange / Act / Assert.
public class LinkPageTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Domain.Frameworks.Framework Cra = NewFramework("CRA – Regulation (EU) 2024/2847");
    private static readonly string CraSub = FrameworkNames.Slug(Cra.Name);

    private static Domain.Frameworks.Framework NewFramework(string name)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return Domain.Frameworks.Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("f.pdf", bytes), Anna, Monday);
    }

    private static Requirement Requirement(string code) => Cra.LatestVersion.AddRequirement(code, "Title " + code, null);

    private static WorkItem Story(string title)
        => WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem(title.Split(' ')[0], "https://github.com/x/y/issues/1", title, "", "", "", "user-story", false), Monday);

    private static Evidence File(string title) => Evidence.ForLink(Project, title, "https://example.org/" + title.Replace(' ', '-'), null, Anna, null, Monday);

    private static LinkedWorkItem Linked(Requirement requirement, WorkItem story)
        => new(RequirementLink.Create(Project, requirement.Id, story.Id, Anna, Monday), story);

    private static SuggestedWorkItem Suggested(Requirement requirement, WorkItem story)
        => new(MappingSuggestion.Create(Project, story.Id, requirement.Id, 0.8, 1, "m", "v", Monday), story);

    private static SuggestedEvidence SuggestedFile(Requirement requirement, Evidence evidence)
        => new(EvidenceLinkSuggestion.Create(Project, evidence.Id, requirement.Id, 0.8, 1, "m", "v", Monday), evidence);

    private static ProjectCompliance Compliance(IReadOnlyList<RequirementCoverage> rows, IReadOnlyList<WorkItem>? stories = null)
        => new([new FrameworkCompliance(Cra, Cra.LatestVersion, rows)], stories ?? []);

    private static LinkPage Build(ProjectCompliance compliance, IReadOnlyList<Evidence>? evidence = null, string? req = null, string? show = null,
        string? q = null, string? sq = null, string? eq = null)
        => LinkPage.Build(compliance, evidence ?? [], CraSub, req, show, q, sq, eq)!;

    // ---------------------------------------------------------- R1: linked first, then the AI's suggestions, then the rest

    [Fact]
    public void A_requirement_shows_its_linked_product_requirements_then_the_suggestions_then_the_rest()
    {
        var requirement = Requirement("I.2(d)");
        var mfa = Story("#42 Log in with MFA");
        var lockout = Story("#57 Lock account after 5 attempts");
        var other = Story("#60 Export orders");
        var row = new RequirementCoverage(requirement, [Linked(requirement, mfa)]) { Suggestions = [Suggested(requirement, lockout)] };

        var page = Build(Compliance([row], [mfa, lockout, other]), req: "I.2(d)", show: ComplianceFilters.All);

        Assert.Equal(["#42 Log in with MFA"], page.LinkedStories.Select(l => l.WorkItem.Title));
        Assert.Equal(["#57 Lock account after 5 attempts"], page.SuggestedStories.Select(s => s.WorkItem.Title));
        Assert.Equal(["#60 Export orders"], page.OtherStories.Select(s => s.Title)); // each story in one place only
    }

    [Fact]
    public void A_requirement_with_no_candidates_has_three_empty_lists_and_can_still_be_linked()
    {
        var page = Build(Compliance([new RequirementCoverage(Requirement("I.1"), [])]), req: "I.1", show: ComplianceFilters.All);

        Assert.True(page.CanLinkChosen);
        Assert.Empty(page.LinkedStories);
        Assert.Empty(page.OtherStories);
        Assert.Empty(page.OtherEvidence);
        Assert.Equal(0, page.MoreStories);
    }

    [Fact]
    public void With_200_candidates_the_rest_is_cut_off_and_a_search_finds_the_one_that_is_cut_off()
    {
        var stories = Enumerable.Range(1, 200).Select(n => Story($"#{n:000} Story")).ToList();
        var compliance = Compliance([new RequirementCoverage(Requirement("I.3"), [])], stories);

        var page = Build(compliance, req: "I.3", show: ComplianceFilters.All);
        var searched = Build(compliance, req: "I.3", show: ComplianceFilters.All, sq: "#199");

        Assert.Equal(LinkPage.MaxOthers, page.OtherStories.Count);
        Assert.Equal(200 - LinkPage.MaxOthers, page.MoreStories);
        Assert.Equal("#001 Story", page.OtherStories[0].Title); // in title order
        Assert.Equal(["#199 Story"], searched.OtherStories.Select(s => s.Title));
        Assert.Equal(0, searched.MoreStories);
    }

    [Fact]
    public void A_removed_story_is_not_offered_for_linking()
    {
        var removed = Story("#9 Old story");
        typeof(WorkItem).GetProperty(nameof(WorkItem.State))!.SetValue(removed, WorkItemState.Removed);

        var page = Build(Compliance([new RequirementCoverage(Requirement("I.4"), [])], [removed]), req: "I.4", show: ComplianceFilters.All);

        Assert.Empty(page.OtherStories);
    }

    [Fact]
    public void Evidence_linked_to_two_requirements_shows_as_linked_on_each_and_is_offered_to_a_third()
    {
        var report = File("MFA test report");
        var first = Requirement("I.5");
        var second = Requirement("I.6");
        var third = Requirement("I.7");
        RequirementCoverage With(Requirement r) => new(r, []) { Evidence = [new EvidenceItem(report, false)] };
        var compliance = Compliance([With(first), With(second), new RequirementCoverage(third, [])]);

        Assert.Equal([report.Id], Build(compliance, [report], "I.5", ComplianceFilters.All).LinkedEvidence.Select(e => e.Evidence.Id));
        Assert.Equal([report.Id], Build(compliance, [report], "I.6", ComplianceFilters.All).LinkedEvidence.Select(e => e.Evidence.Id));
        var onThird = Build(compliance, [report], "I.7", ComplianceFilters.All);
        Assert.Empty(onThird.LinkedEvidence);
        Assert.Equal([report.Id], onThird.OtherEvidence.Select(e => e.Id));
    }

    [Fact]
    public void Suggested_evidence_is_not_also_in_the_rest_and_a_search_narrows_the_rest()
    {
        var requirement = Requirement("I.8");
        var pen = File("Pen test 2026");
        var scan = File("Weekly scan");
        var guide = File("Hardening guide");
        var row = new RequirementCoverage(requirement, []) { SuggestedEvidence = [SuggestedFile(requirement, pen)] };

        var page = Build(Compliance([row]), [pen, scan, guide], "I.8", ComplianceFilters.All);
        var searched = Build(Compliance([row]), [pen, scan, guide], "I.8", ComplianceFilters.All, eq: "  SCAN ");

        Assert.Equal(["Pen test 2026"], page.SuggestedEvidence.Select(s => s.Evidence.Title));
        Assert.Equal(["Hardening guide", "Weekly scan"], page.OtherEvidence.Select(e => e.Title));
        Assert.Equal(["Weekly scan"], searched.OtherEvidence.Select(e => e.Title));
    }

    // ---------------------------------------------------------- R4: the marks

    [Fact]
    public void Unlinking_the_last_product_requirement_makes_the_requirement_not_covered()
    {
        var requirement = Requirement("I.9");
        var linked = new RequirementCoverage(requirement, [Linked(requirement, Story("#1 Story"))]);
        var unlinked = new RequirementCoverage(requirement, []);

        Assert.Equal(LinkMarks.Partly, LinkMarks.Of(linked)); // a story, but no evidence yet
        Assert.Equal(LinkMarks.NotCovered, LinkMarks.Of(unlinked));
        Assert.Contains(Build(Compliance([unlinked])).Requirements, r => r.Row.Requirement.Code == "I.9");
        Assert.DoesNotContain(Build(Compliance([linked])).Requirements, r => r.Row.Requirement.Code == "I.9"); // "Not covered" is the start
    }

    [Fact]
    public void A_product_requirement_and_evidence_that_counts_make_it_covered_and_expired_evidence_does_not()
    {
        var requirement = Requirement("I.10");
        var story = Story("#1 Story");
        var report = File("Report");

        Assert.Equal(LinkMarks.Covered, LinkMarks.Of(new RequirementCoverage(requirement, [Linked(requirement, story)]) { Evidence = [new EvidenceItem(report, false)] }));
        Assert.Equal(LinkMarks.Partly, LinkMarks.Of(new RequirementCoverage(requirement, [Linked(requirement, story)]) { Evidence = [new EvidenceItem(report, true)] }));
        Assert.Equal(LinkMarks.Partly, LinkMarks.Of(new RequirementCoverage(requirement, []) { Evidence = [new EvidenceItem(report, false)] }));
    }

    // ---------------------------------------------------------- R5: not applicable

    [Fact]
    public void A_requirement_marked_not_applicable_shows_its_reason_and_offers_nothing_to_link()
    {
        var requirement = Requirement("I.11");
        var row = new RequirementCoverage(requirement, [], "No radio in the product")
        {
            Suggestions = [Suggested(requirement, Story("#2 Story"))],
            SuggestedEvidence = [SuggestedFile(requirement, File("Scan"))]
        };

        var page = Build(Compliance([row], [Story("#3 Story")]), req: "I.11");

        Assert.False(page.CanLinkChosen);
        Assert.Equal("No radio in the product", page.Chosen?.NotApplicableReason);
        Assert.Empty(page.SuggestedStories);
        Assert.Empty(page.SuggestedEvidence);
        Assert.Equal(LinkMarks.NotApplicable, LinkMarks.Of(row));
        Assert.False(LinkPage.Suggests(row)); // its suggestions do not count for "Has suggestions"
    }

    // ---------------------------------------------------------- R6: the filters and the search

    [Theory]
    [InlineData(null, ComplianceFilters.NotCovered)]
    [InlineData("", ComplianceFilters.NotCovered)]
    [InlineData("ALL", ComplianceFilters.All)]
    [InlineData(" has-suggestions ", ComplianceFilters.HasSuggestions)]
    [InlineData("needs-review", ComplianceFilters.NotCovered)] // the Compliance tab's other filters are not on this page
    [InlineData("nonsense", ComplianceFilters.NotCovered)]
    public void The_page_opens_on_not_covered_and_an_unknown_filter_is_not_covered(string? show, string expected)
        => Assert.Equal(expected, LinkPage.FilterOf(show));

    [Fact]
    public void Has_suggestions_shows_the_requirements_with_a_suggested_story_or_evidence()
    {
        var story = Requirement("I.12");
        var file = Requirement("I.13");
        var none = Requirement("I.14");
        var rows = new[]
        {
            new RequirementCoverage(story, []) { Suggestions = [Suggested(story, Story("#5 Story"))] },
            new RequirementCoverage(file, []) { SuggestedEvidence = [SuggestedFile(file, File("Scan"))] },
            new RequirementCoverage(none, [])
        };

        var page = Build(Compliance(rows), show: ComplianceFilters.HasSuggestions);

        Assert.Equal(["I.12", "I.13"], page.Requirements.Select(r => r.Row.Requirement.Code));
        Assert.All(page.Requirements, r => Assert.True(r.HasSuggestions));
    }

    [Fact]
    public void The_search_finds_requirements_by_code_or_title_whatever_the_case()
    {
        var rows = new[] { new RequirementCoverage(Requirement("II.1"), []), new RequirementCoverage(Requirement("II.2"), []) };

        Assert.Equal(["II.2"], Build(Compliance(rows), q: "title ii.2").Requirements.Select(r => r.Row.Requirement.Code));
        Assert.Equal(2, Build(Compliance(rows), q: "   ").Requirements.Count);
    }

    // ---------------------------------------------------------- Choosing a requirement

    [Fact]
    public void Without_a_requirement_in_the_address_the_first_one_shown_is_chosen_and_an_empty_list_chooses_none()
    {
        var rows = new[] { new RequirementCoverage(Requirement("III.1"), []), new RequirementCoverage(Requirement("III.2"), []) };

        Assert.Equal("III.1", Build(Compliance(rows)).Chosen?.Requirement.Code);
        var none = Build(Compliance(rows), q: "no such requirement");
        Assert.Null(none.Chosen);
        Assert.Empty(none.OtherStories);
    }

    [Fact]
    public void A_chosen_requirement_stays_chosen_when_the_filter_hides_it()
    {
        var requirement = Requirement("III.3");
        var covered = new RequirementCoverage(requirement, [Linked(requirement, Story("#1 Story"))]) { Evidence = [new EvidenceItem(File("Report"), false)] };

        var page = Build(Compliance([covered]), req: "iii.3"); // just linked, so "Not covered" no longer lists it

        Assert.Equal("III.3", page.Chosen?.Requirement.Code);
        Assert.Empty(page.Requirements);
    }

    [Fact]
    public void After_a_framework_version_change_an_old_code_or_an_unknown_framework_finds_nothing()
    {
        var page = LinkPage.Build(Compliance([new RequirementCoverage(Requirement("IV.1"), [])]), [], CraSub, "Gone.1", null, null, null, null);
        var unknown = LinkPage.Build(Compliance([new RequirementCoverage(Requirement("IV.2"), [])]), [], "no-such-framework", null, null, null, null, null);
        var first = LinkPage.Build(Compliance([new RequirementCoverage(Requirement("IV.3"), [])]), [], null, null, null, null, null, null);

        Assert.Null(page?.Chosen); // the controller answers "not found"
        Assert.Null(unknown);
        Assert.Equal(CraSub, first?.Sub); // no framework in the address: the first
    }
}
