using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// Unit tests of the risk agent's prompt, its answer and the drafts (US-112). Every test follows Arrange / Act / Assert.
public class RiskDraftTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();

    private static DraftedRisk Drafted(string title) => new(title, "threat", "asset", 3, 3, "based on", "why");

    // ---------------------------------------------------------- Reading the answer

    [Fact]
    public void Each_block_becomes_a_draft_with_its_fields()
    {
        var drafts = RiskDraftPrompt.Parse("RISK: Stolen cookie\nTHREAT: XSS\nASSET: Accounts\nLIKELIHOOD: 3\nIMPACT: 4\nBASED ON: I.2(d)\nWHY: Not covered\n---\nRISK: Old TLS\nLIKELIHOOD: 2\nIMPACT: 3");

        Assert.Equal(2, drafts.Count);
        Assert.Equal(new DraftedRisk("Stolen cookie", "XSS", "Accounts", 3, 4, "I.2(d)", "Not covered"), drafts[0]);
        Assert.Equal(("Old TLS", "", 2, 3), (drafts[1].Title, drafts[1].Threat, drafts[1].Likelihood, drafts[1].Impact));
    }

    [Theory]
    [InlineData("4 (high)", 4)]
    [InlineData("9", 5)]
    [InlineData("0", 1)]
    [InlineData("High", 4)]
    [InlineData("very low", 1)]
    [InlineData("critical", 5)]
    [InlineData("unclear", 3)]
    public void A_scale_in_words_or_out_of_range_becomes_1_to_5(string text, int expected)
        => Assert.Equal(expected, RiskDraftPrompt.Parse($"RISK: A\nLIKELIHOOD: {text}\nIMPACT: 3").Single().Likelihood);

    [Fact]
    public void A_field_may_run_over_several_lines_and_a_block_without_a_title_is_left_out()
    {
        var drafts = RiskDraftPrompt.Parse("THREAT: no title\n---\nRISK: Phishing\nWHY: Staff get many mails\nand click links");

        Assert.Equal("Staff get many mails and click links", drafts.Single().Reasoning);
    }

    [Fact]
    public void At_most_10_drafts_are_read()
        => Assert.Equal(10, RiskDraftPrompt.Parse(string.Join("\n---\n", Enumerable.Range(1, 15).Select(i => $"RISK: Risk {i}"))).Count);

    [Fact]
    public void The_prompt_has_the_facts_and_says_none_where_there_are_none()
    {
        var prompt = RiskDraftPrompt.For(new RiskDraftContext("A customer portal", null, ["CRA: Important, class I"], ["CRA I.2(d) Access"], [], [], ["Phishing"]));

        Assert.Contains("Intended purpose: A customer portal", prompt);
        Assert.Contains("- CRA: Important, class I", prompt);
        Assert.Contains("Open security events:\n(none)", prompt.Replace("\r\n", "\n"));
        Assert.Contains("- Phishing", prompt);
    }

    // ---------------------------------------------------------- The same risk (R3, R5)

    [Theory]
    [InlineData("Stolen session cookie", "Session cookie stolen", true)]
    [InlineData("Phishing", "Phishing of staff", true)]
    [InlineData("Stolen session cookie", "Unpatched component", false)]
    [InlineData("The", "the", false)]
    public void Two_titles_name_the_same_risk_when_most_words_are_shared(string a, string b, bool expected)
        => Assert.Equal(expected, RiskDraftPrompt.Same(a, b));

    [Fact]
    public void A_draft_an_existing_risk_or_a_discarded_draft_covers_is_left_out()
    {
        var risk = Risk.Create(Project, "Stolen session cookie", null, 3, 4, Anna, RiskTreatment.Reduce, Anna, Monday);
        var discarded = RiskDraft.Create(Project, "Unpatched component", "t", "a", 2, 4, "b", "w", "m", "v1", Monday);
        discarded.Discard("We have Dependabot", Anna, null, Monday);

        var (kept, leftOut) = RiskDraftService.Keep([Drafted("Session cookie stolen"), Drafted("Unpatched component"), Drafted("Phishing")], [risk], [discarded]);

        Assert.Equal(["Phishing"], kept.Select(k => k.Title));
        Assert.Equal(["the risk \"Stolen session cookie\"", "the discarded draft \"Unpatched component\""], leftOut.Select(l => l.CoveredBy));
    }

    [Fact]
    public void The_same_risk_twice_in_one_answer_is_kept_once()
    {
        var (kept, leftOut) = RiskDraftService.Keep([Drafted("Phishing of staff"), Drafted("Phishing")], [], []);

        Assert.Single(kept);
        Assert.Single(leftOut);
    }

    // ---------------------------------------------------------- The draft (R1, R4, R5)

    [Fact]
    public void A_draft_needs_a_title_and_keeps_its_scale_within_1_to_5()
    {
        Assert.Throws<DomainException>(() => RiskDraft.Create(Project, " ", "t", "a", 3, 3, "b", "w", "m", "v1", Monday));

        var draft = RiskDraft.Create(Project, "Phishing", "t", "a", 9, 0, "b", "w", "m", "v1", Monday);

        Assert.Equal((5, 1, RiskLevel.Medium), (draft.Likelihood, draft.Impact, draft.Level));
    }

    [Fact]
    public void A_draft_is_accepted_or_discarded_once_and_discarding_needs_a_reason()
    {
        var draft = RiskDraft.Create(Project, "Phishing", "t", "a", 3, 3, "b", "w", "m", "v1", Monday);
        Assert.Equal("Give a reason for discarding the draft", Assert.Throws<DomainException>(() => draft.Discard(" ", Anna, null, Monday)).Message);

        var risk = Guid.NewGuid();
        draft.Accept(risk, Anna, null, Monday);

        Assert.Equal((RiskDraftStatus.Accepted, risk), (draft.Status, draft.RiskId));
        Assert.Throws<DomainException>(() => draft.Discard("late", Anna, null, Monday));
    }

    [Fact]
    public void A_risk_from_a_draft_is_marked_AI_drafted()
    {
        var risk = Risk.Create(Project, "Phishing", null, 3, 3, Anna, RiskTreatment.Reduce, Anna, Monday);

        risk.FromAiDraft(Guid.NewGuid());

        Assert.True(risk.IsAiDrafted);
    }
}
