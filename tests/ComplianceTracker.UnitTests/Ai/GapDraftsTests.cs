using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.UnitTests.Ai;

// US-108: drafts for a regulation's gaps. Every test follows Arrange / Act / Assert.
public class GapDraftsTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Codes = ["I.2(d)", "I.2(e)", "I.2(i)", "I.2(m)"];

    private const string TwoStories = """
        DRAFT
        COVERS: I.2(d), I.2(e)
        TITLE: Sign in with MFA
        ROLE: Customer
        STORY: As a customer, I want to sign in with MFA, so that nobody else sees my orders
        RULES: R1: a one-time code is needed
        R2: the session is encrypted
        EXAMPLES: Wrong code → refused
        FITS: Customers' orders are private
        END
        DRAFT
        COVERS: I.2(e), I.2(m)
        TITLE: Delete my account
        ROLE: Customer
        STORY: As a customer, I want to delete my account, so that nothing about me is left
        RULES: R1: all data is removed
        EXAMPLES: Delete → gone
        FITS: The portal stores customer data
        END
        NOT APPLICABLE: I.2(i): The portal has no network of its own
        """;

    // ---------------------------------------------------------- The answer (R2–R5)

    [Fact]
    public void Drafts_and_not_applicable_are_read_and_a_code_is_used_once()
    {
        var answer = GapDraftPrompt.Parse(TwoStories, DraftForm.UserStory, Codes);

        Assert.Equal(2, answer.Drafts.Count);
        Assert.Equal(["I.2(d)", "I.2(e)"], answer.Drafts[0].Covers);
        Assert.Equal(["I.2(m)"], answer.Drafts[1].Covers); // I.2(e) went to the first draft
        Assert.Equal("R1: a one-time code is needed\nR2: the session is encrypted", answer.Drafts[0].Rules);
        Assert.Equal("Customers' orders are private", answer.Drafts[0].Fits);
        Assert.Equal("The portal has no network of its own", answer.NotApplicable["I.2(i)"]);
    }

    [Fact]
    public void A_use_case_is_read_into_its_sections()
    {
        const string text = """
            DRAFT
            COVERS: I.2(d)
            TITLE: UC-12 Sign in
            ACTOR: Radiologist
            PRECONDITIONS: Has an account
            SCENARIO: 1. Opens the portal
            2. Enters the one-time code
            EXTENSIONS: 2a. Wrong code
            POSTCONDITIONS: Signed in
            FITS: The tool shows patient images
            END
            """;

        var draft = GapDraftPrompt.Parse(text, DraftForm.UseCase, Codes).Drafts.Single();

        Assert.Equal(("Radiologist", "Has an account", "1. Opens the portal\n2. Enters the one-time code", "2a. Wrong code", "Signed in"),
            (draft.Role, draft.Rules, draft.Story, draft.Examples, draft.Postconditions));
    }

    [Fact]
    public void A_requirement_has_a_description_and_acceptance_criteria()
    {
        var draft = GapDraftPrompt.Parse("DRAFT\nCOVERS: I.2(m)\nTITLE: REQ-9 Delete data\nDESCRIPTION: The portal shall delete\nACCEPTANCE: A test shows it\nEND",
            DraftForm.Requirement, Codes).Drafts.Single();

        Assert.Equal(("", "The portal shall delete", "A test shows it", ""), (draft.Role, draft.Story, draft.Rules, draft.Examples));
    }

    [Theory]
    [InlineData("DRAFT\nCOVERS: A.9.9\nTITLE: x\nSTORY: y\nEND")]       // a code that is not a gap
    [InlineData("DRAFT\nCOVERS: I.2(d)\nSTORY: y\nEND")]               // no title
    [InlineData("DRAFT\nCOVERS: I.2(d)\nTITLE: x\nEND")]               // no story
    [InlineData("Here are some ideas for the portal.")]                // no block at all
    public void A_draft_without_a_known_code_a_title_or_a_body_is_left_out(string text)
        => Assert.Empty(GapDraftPrompt.Parse(text, DraftForm.UserStory, Codes).Drafts);

    [Fact]
    public void At_most_three_requirements_share_one_draft()
        => Assert.Equal(3, GapDraftPrompt.Parse("DRAFT\nCOVERS: I.2(d), I.2(e), I.2(i), I.2(m)\nTITLE: x\nSTORY: y\nEND", DraftForm.UserStory, Codes)
            .Drafts.Single().Covers.Count);

    [Fact]
    public void A_block_without_END_at_the_end_still_counts_and_a_drafted_code_is_not_also_not_applicable()
    {
        var answer = GapDraftPrompt.Parse("NOT APPLICABLE: I.2(d): no\nDRAFT\nCOVERS: I.2(d)\nTITLE: x\nSTORY: y", DraftForm.UserStory, Codes);

        Assert.Single(answer.Drafts);
        Assert.Empty(answer.NotApplicable);
    }

    // ---------------------------------------------------------- The form (R2)

    [Theory]
    [InlineData(RequirementMethods.GitHubUserStories, new[] { DraftForm.UserStory })]
    [InlineData(RequirementMethods.UseCases, new[] { DraftForm.UseCase })]
    [InlineData(RequirementMethods.All, new[] { DraftForm.UserStory, DraftForm.UseCase, DraftForm.Requirement })]
    [InlineData(RequirementMethods.None, new[] { DraftForm.UserStory })]
    public void The_forms_follow_the_project_s_methods(RequirementMethods methods, DraftForm[] forms)
        => Assert.Equal(forms, DraftForms.Of(methods));

    [Fact]
    public void With_several_methods_a_person_chooses_and_a_form_not_used_is_refused()
    {
        Assert.Equal("Choose the form of the drafts", Assert.Throws<DomainException>(() => DraftForms.Choose(RequirementMethods.All, null)).Message);
        Assert.Equal(DraftForm.UseCase, DraftForms.Choose(RequirementMethods.All, DraftForm.UseCase));
        Assert.Throws<DomainException>(() => DraftForms.Choose(RequirementMethods.GitHubUserStories, DraftForm.UseCase));
    }

    // ---------------------------------------------------------- The draft (R4, R6, R7)

    private static StoryDraft UseCase(params Guid[] covers)
        => StoryDraft.ForGaps(Project, DraftForm.UseCase, covers, "UC-12 Sign in", "Radiologist", "1. Opens", "Has an account", "2a. Wrong code",
            "Signed in", "Fits", "model", GapDraftPrompt.Version, Monday);

    [Fact]
    public void A_draft_covers_one_to_three_requirements()
    {
        Assert.Throws<DomainException>(() => UseCase());
        Assert.Equal("A draft covers at most 3 requirements", Assert.Throws<DomainException>(() => UseCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())).Message);

        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal([a, b], UseCase(a, b, a).RequirementIds);
    }

    [Fact]
    public void A_use_case_needs_a_name_and_a_main_success_scenario()
        => Assert.Equal("A draft needs a name and a main success scenario", Assert.Throws<DomainException>(() =>
            StoryDraft.ForGaps(Project, DraftForm.UseCase, [Guid.NewGuid()], "UC-12", "", " ", "", "", "", null, "m", "v", Monday)).Message);

    [Fact]
    public void Changing_only_the_postconditions_counts_as_edited()
    {
        var draft = UseCase(Guid.NewGuid());

        draft.Accept(draft.Title, draft.Role, draft.Story, draft.Rules, draft.Examples, Anna, Monday, null, "Signed in and logged");

        Assert.Equal((StoryDraftStatus.Accepted, true, "Signed in and logged"), (draft.Status, draft.EditedByPerson, draft.Postconditions));
    }

    [Fact]
    public void A_created_use_case_is_text_for_the_team_s_document()
    {
        var text = UseCase(Guid.NewGuid()).CopyText(["CRA I.2(d)", "CRA I.2(e)"]);

        Assert.Contains("Primary actor: Radiologist", text);
        Assert.Contains("Main success scenario:\n1. Opens", text.Replace("\r\n", "\n"));
        Assert.Contains("Covers: CRA I.2(d), CRA I.2(e)", text);
    }

    // ---------------------------------------------------------- The gaps (R1, R8)

    private static (FrameworkCompliance Framework, Requirement Covered, Requirement Gap, Requirement Suggested, Requirement Recommended) Framework()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Domain.Frameworks.Framework.Create(Guid.NewGuid(), "CRA demo", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        var covered = framework.LatestVersion.AddRequirement("I.2(a)", "Covered", null);
        var gap = framework.LatestVersion.AddRequirement("I.2(b)", "Gap", null);
        var suggested = framework.LatestVersion.AddRequirement("I.2(c)", "Has a suggestion", null);
        var recommended = framework.LatestVersion.AddRequirement("I.2(z)", "Recommended", null, RequirementKind.Recommendation);
        var story = Domain.WorkItems.WorkItem.Create(Project, Domain.WorkItems.WorkItemSource.GitHub,
            new Domain.WorkItems.ExternalWorkItem("#1", "https://github.com/x/y/issues/1", "Story", "", "", "", "user-story", false), Monday);
        var suggestion = MappingSuggestion.Create(Project, story.Id, suggested.Id, 0.8, 1, "m", "v", Monday);
        var rows = new List<RequirementCoverage>
        {
            new(covered, [new LinkedWorkItem(RequirementLink.Create(Project, covered.Id, story.Id, Anna, Monday), story)]),
            new(gap, []),
            new(suggested, []) { Suggestions = [new SuggestedWorkItem(suggestion, story)] },
            new(recommended, [])
        };
        return (new FrameworkCompliance(framework, framework.LatestVersion, rows), covered, gap, suggested, recommended);
    }

    [Fact]
    public void Only_the_uncovered_requirements_without_a_matching_story_are_gaps()
    {
        var (framework, _, gap, _, _) = Framework();

        Assert.Equal([gap.Id], GapDraftService.Gaps(framework, [], DraftForm.UserStory).Select(r => r.Id));
    }

    [Fact]
    public void A_requirement_with_an_open_draft_is_not_drafted_again_and_a_discarded_one_only_in_another_form()
    {
        var (framework, _, gap, _, _) = Framework();
        var open = StoryDraft.ForGaps(Project, DraftForm.UserStory, [gap.Id], "x", "r", "s", "", "", "", null, "m", "v", Monday);
        var discarded = StoryDraft.ForGaps(Project, DraftForm.UserStory, [gap.Id], "x", "r", "s", "", "", "", null, "m", "v", Monday);
        discarded.Discard("Not now", Anna, Monday);

        Assert.Empty(GapDraftService.Gaps(framework, [open], DraftForm.UserStory));
        Assert.Empty(GapDraftService.Gaps(framework, [discarded], DraftForm.UserStory));
        Assert.Equal([gap.Id], GapDraftService.Gaps(framework, [discarded], DraftForm.UseCase).Select(r => r.Id));
    }
}
