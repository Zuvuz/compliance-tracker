using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.UnitTests.Ai;

// US-205: the check that reads a document against a requirement and quotes it. Every test follows Arrange / Act / Assert.
public class PassageCheckingTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private const string Procedure = "Incidents are reported to CFCS within 24 hours.\n\nThe team meets every week to look at the logs.";

    private static Requirement Requirement(string code = "Art.14(3)")
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA demo", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        return framework.LatestVersion.AddRequirement(code, "Severe incidents are notified", "Notify the CSIRT and ENISA");
    }

    private sealed class FakeWriter(string? answer, bool down = false) : IRationaleWriter, IAiModels
    {
        public string Name => "qwen3:14b";
        public string? Prompt { get; private set; }

        public Task<string> WriteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            return down ? throw new AiUnavailableException("down") : Task.FromResult(answer!);
        }

        public IEmbeddingModel Embedding(AiTask task) => throw new NotSupportedException();
        public IRationaleWriter Writer(AiTask task) => this;
        public IReranker? Reranker() => null;
        public IReadOnlyList<AiTaskModel> Overview() => [];
    }

    private static Task<CheckResult> CheckAsync(string? answer, string? text = Procedure, bool down = false)
        => new PassageChecker(new FakeWriter(answer, down)).CheckAsync(Requirement(), "Incident handling procedure", text, DocumentSections.Of(text));

    // ---------------------------------------------------------- The answer (R1, R2, R4)

    [Fact]
    public async Task Partly_with_a_quote_from_the_document_is_a_checked_suggestion()
    {
        var result = await CheckAsync("VERDICT: Partly meets\nPASSAGE: \"Incidents are reported to CFCS within 24 hours\"\nMISSING: who reports to ENISA");

        Assert.Equal(CheckOutcome.Checked, result.Outcome);
        Assert.Equal((PassageVerdict.Partly, "Incidents are reported to CFCS within 24 hours", "who reports to ENISA", "qwen3:14b"),
            (result.Check!.Verdict, result.Check.Passage, result.Check.Missing, result.Check.Model));
    }

    [Fact]
    public async Task Meets_has_nothing_missing()
    {
        var result = await CheckAsync("**VERDICT:** Meets.\nPASSAGE: Incidents are reported to CFCS within 24 hours\nMISSING: Nothing");

        Assert.Equal((PassageVerdict.Meets, null), (result.Check!.Verdict, result.Check.Missing));
    }

    [Theory]
    [InlineData("VERDICT: Does not meet\nPASSAGE: \nMISSING: everything")]
    [InlineData("VERDICT: Not met\nPASSAGE: Incidents are reported to CFCS within 24 hours")]
    public async Task A_document_only_on_the_same_topic_is_not_suggested(string answer)
        => Assert.Equal((CheckOutcome.NotMet, false), ((await CheckAsync(answer)).Outcome, (await CheckAsync(answer)).Suggest));

    [Fact]
    public async Task A_quote_that_is_not_in_the_document_is_dropped()
        => Assert.Equal(CheckOutcome.QuoteMissing, (await CheckAsync("VERDICT: Meets\nPASSAGE: We report every incident to ENISA")).Outcome);

    [Fact]
    public async Task A_meets_without_a_quote_is_dropped()
        => Assert.Equal(CheckOutcome.QuoteMissing, (await CheckAsync("VERDICT: Meets\nMISSING: Nothing")).Outcome);

    // ---------------------------------------------------------- Not checked (R7)

    [Theory]
    [InlineData("I think the document is fine")]
    [InlineData("")]
    [InlineData("VERDICT: maybe")]
    public async Task An_answer_in_a_wrong_format_is_made_as_not_checked(string answer)
        => Assert.Equal((CheckOutcome.Unchecked, true), ((await CheckAsync(answer)).Outcome, (await CheckAsync(answer)).Suggest));

    [Fact]
    public async Task The_AI_off_is_not_checked()
        => Assert.Equal(CheckOutcome.Unchecked, (await CheckAsync(null, down: true)).Outcome);

    [Fact]
    public async Task A_document_without_text_is_not_checked_and_the_model_is_not_asked()
    {
        var writer = new FakeWriter("VERDICT: Meets\nPASSAGE: x");

        var result = await new PassageChecker(writer).CheckAsync(Requirement(), "Scanned procedure", null, []);

        Assert.Equal(CheckOutcome.Unchecked, result.Outcome);
        Assert.Null(writer.Prompt);
    }

    [Fact]
    public async Task The_prompt_holds_the_requirement_the_title_and_the_sections()
    {
        var writer = new FakeWriter("VERDICT: Does not meet");

        await new PassageChecker(writer).CheckAsync(Requirement(), "Incident handling procedure", Procedure, ["Section one", "Section two"]);

        Assert.Contains(PassageCheckPrompt.Marker, writer.Prompt);
        Assert.Contains("Requirement Art.14(3): Severe incidents are notified", writer.Prompt);
        Assert.Contains("Section one", writer.Prompt);
        Assert.Contains("Section two", writer.Prompt);
    }

    // ---------------------------------------------------------- The quote (R3)

    [Theory]
    [InlineData("incidents are reported   to CFCS\nwithin 24 hours", true)]
    [InlineData("“Incidents are reported to CFCS within 24 hours.”…", false)]
    [InlineData("Incidents are reported to CFCS within 24 hours…", true)]
    [InlineData("reported to CFCS", true)]
    [InlineData("CFCS", false)]
    [InlineData("Incidents are reported to ENISA", false)]
    public void The_quote_must_be_in_the_document_word_for_word(string quote, bool found)
        => Assert.Equal(found, QuoteCheck.IsIn(Procedure, quote));

    [Fact]
    public void No_text_or_no_quote_is_never_found()
    {
        Assert.False(QuoteCheck.IsIn(null, "Incidents are reported"));
        Assert.False(QuoteCheck.IsIn(Procedure, "  "));
    }

    // ---------------------------------------------------------- Sections (R5)

    [Fact]
    public void A_long_document_is_split_into_sections_of_at_most_the_length()
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"Paragraph {i}: " + new string('x', 100)));

        var sections = DocumentSections.Of(text, 500);

        Assert.True(sections.Count > 1);
        Assert.All(sections, s => Assert.True(s.Length <= 500));
        Assert.Contains("Paragraph 30", sections[^1]);
    }

    [Fact]
    public void A_paragraph_longer_than_a_section_is_split_too()
    {
        var sections = DocumentSections.Of(new string('y', 1300), 500);

        Assert.Equal([500, 500, 300], sections.Select(s => s.Length));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void No_text_has_no_sections(string? text) => Assert.Empty(DocumentSections.Of(text));

    [Fact]
    public void The_check_reads_the_sections_that_share_most_words_with_the_requirement()
    {
        string[] sections = ["Backups run every night.", "The office has a kitchen.", "Incidents are reported to the CSIRT.", "Access is logged.", "Severe incidents notified quickly."];

        var best = PassageChecker.BestSections(sections, null, null, "Severe incidents are notified to the CSIRT");

        Assert.Equal(3, best.Count);
        Assert.Contains("Incidents are reported to the CSIRT.", best);
        Assert.Contains("Severe incidents notified quickly.", best);
    }

    // ---------------------------------------------------------- The stored check (R2, R10)

    [Fact]
    public void A_check_needs_a_passage_and_a_model_and_keeps_missing_only_for_partly()
    {
        Assert.Throws<DomainException>(() => new PassageCheck(PassageVerdict.Meets, " ", null, "m"));
        Assert.Throws<DomainException>(() => new PassageCheck(PassageVerdict.Meets, "x", null, " "));
        Assert.Null(new PassageCheck(PassageVerdict.Meets, "x", "something", "m").Missing);
        Assert.Equal(PassageCheck.MaxPassageLength, new PassageCheck(PassageVerdict.Partly, new string('q', 900), null, "m").Passage.Length);
    }

    [Fact]
    public void A_suggestion_remembers_its_check_and_says_which_model()
    {
        var suggestion = DocumentLinkSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "I.2(d)", 0.8, 1, "embed", "v", Monday);
        Assert.False(suggestion.IsChecked);

        suggestion.Checked(new PassageCheck(PassageVerdict.Partly, "Reported to CFCS", "ENISA", "qwen3:14b"));

        Assert.Equal((true, PassageVerdict.Partly, "Reported to CFCS", "ENISA", "qwen3:14b"),
            (suggestion.IsChecked, suggestion.CheckVerdict!.Value, suggestion.CheckPassage, suggestion.CheckMissing, suggestion.CheckModel));
    }
}
