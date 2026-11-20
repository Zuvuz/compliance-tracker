using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Reports;

namespace ComplianceTracker.UnitTests.Reports;

// Edge-case unit tests of US-040: the texts, their data and fingerprint, the AI's answer and when a report can be locked.
// Arrange / Act / Assert.
public class StatusReportTests
{
    private const string Cra = "CRA – Regulation (EU) 2024/2847";
    private const string Iso = "ISO/IEC 27001:2022 – Information security";
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly ManagementFacts NoFacts = new(0, 0, 0, null);

    private static FrameworkCompliance Framework(string name, int count, int covered)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Domain.Frameworks.Framework.Create(Org, name, CatalogFile.Create("f.pdf", bytes), Anna, Monday, FrameworkLevel.Product);
        var requirements = Enumerable.Range(1, count).Select(i => new RequirementCoverage(framework.LatestVersion.AddRequirement($"R{i}", $"Title {i}", null), [])
        {
            CoveredThrough = i <= covered ? "elsewhere" : null
        }).ToList();
        return new FrameworkCompliance(framework, framework.LatestVersion, requirements);
    }

    private static StatusText Drafted(string part = StatusText.Summary, string basis = "b1")
        => StatusText.Draft(Project, part, "  Half   of it\nis covered. ", basis, "qwen2.5:3b", Monday);

    // ---------------------------------------------------------- The text (R4, R5)

    [Fact]
    public void A_draft_is_AI_generated_and_its_spaces_are_tidied()
    {
        var text = Drafted();

        Assert.Equal(("Half of it is covered.", true, "qwen2.5:3b", true), (text.Text, text.IsAiGenerated, text.Model, text.IsSummary));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    public void An_empty_text_is_refused(string text)
    {
        Assert.Equal("Write a text", Assert.Throws<DomainException>(() => StatusText.Draft(Project, "x", text, "b", "m", Monday)).Message);
        Assert.Throws<DomainException>(() => Drafted().Edit(text, Anna, "b1", Monday));
    }

    [Fact]
    public void A_text_can_be_1500_characters_but_not_more()
    {
        var text = Drafted();

        text.Edit(new string('a', StatusText.MaxLength), Anna, "b1", Monday);

        Assert.Throws<DomainException>(() => text.Edit(new string('a', StatusText.MaxLength + 1), Anna, "b1", Monday));
        Assert.Throws<DomainException>(() => StatusText.Draft(Project, " ", "t", "b", "m", Monday));
    }

    [Fact]
    public void Accepting_removes_the_mark_and_remembers_who()
    {
        var text = Drafted();

        text.Accept(Anna, "b1", Monday.AddHours(1));

        Assert.Equal((false, (Guid?)Anna, (DateTime?)Monday.AddHours(1)), (text.IsAiGenerated, text.ChangedById, text.ChangedAt));
        Assert.Equal("The text is accepted already", Assert.Throws<DomainException>(() => text.Accept(Anna, "b1", Monday)).Message);
    }

    [Fact]
    public void An_outdated_accepted_text_can_be_accepted_again_for_the_new_figures()
    {
        var text = Drafted();
        text.Accept(Anna, "b1", Monday);

        Assert.True(text.IsOutdated("b2"));
        text.Accept(Anna, "b2", Monday.AddDays(1));

        Assert.False(text.IsOutdated("b2"));
    }

    [Fact]
    public void Editing_is_a_person_s_own_words_and_drafting_again_marks_it_again()
    {
        var text = Drafted();

        text.Edit("Our words", Anna, "b2", Monday);
        Assert.Equal(("Our words", false, "b2"), (text.Text, text.IsAiGenerated, text.Basis));

        text.Redraft("New AI words", "b3", "llama3", Monday.AddDays(1));
        Assert.Equal((true, (Guid?)null, "llama3"), (text.IsAiGenerated, text.ChangedById, text.Model));
    }

    // ---------------------------------------------------------- The data (R2, R3)

    [Fact]
    public void A_regulation_s_data_comes_from_the_coverage()
    {
        var data = StatusReportData.Of(Framework(Cra, 4, 1));

        Assert.Equal("CRA version 1: 1 of 4 requirements covered, 0 with confirmed evidence, 3 not covered (R2, R3, R4)", data);
    }

    [Fact]
    public void At_most_ten_gaps_are_named()
    {
        var data = StatusReportData.Of(Framework(Cra, 15, 0));

        Assert.EndsWith("15 not covered (R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, …)", data);
    }

    [Fact]
    public void Nothing_missing_names_no_gaps()
    {
        Assert.EndsWith("0 not covered", StatusReportData.Of(Framework(Cra, 2, 2)));
    }

    [Fact]
    public void The_basis_changes_with_the_figures_and_a_regulation_no_longer_followed_has_none()
    {
        var before = Framework(Cra, 4, 1);
        var after = Framework(Cra, 4, 2);

        Assert.NotEqual(StatusReportData.BasisOf(Cra, [before], NoFacts), StatusReportData.BasisOf(Cra, [after], NoFacts));
        Assert.Equal(StatusReportData.BasisOf(Cra, [before], NoFacts), StatusReportData.BasisOf(Cra, [Framework(Cra, 4, 1)], NoFacts));
        Assert.Null(StatusReportData.BasisOf(Iso, [before], NoFacts));
    }

    [Fact]
    public void The_summary_s_basis_also_changes_with_the_open_risks()
    {
        var cra = Framework(Cra, 4, 1);

        Assert.NotEqual(StatusReportData.BasisOf(StatusText.Summary, [cra], NoFacts),
            StatusReportData.BasisOf(StatusText.Summary, [cra], new ManagementFacts(1, 0, 0, null)));
    }

    // ---------------------------------------------------------- The AI's answer (R3)

    [Fact]
    public void The_answer_gives_the_summary_and_a_text_per_regulation_it_names()
    {
        var frameworks = new[] { Framework(Cra, 2, 1), Framework(Iso, 2, 1) };

        var answer = StatusReportPrompt.Parse("Here it is:\n**SUMMARY: Half is covered.\nTEXT cra: Some.\nTEXT NIS2: Unknown.\n- TEXT ISO/IEC 27001:2022:  Most. \nTEXT ISO/IEC 27001:2022: ", frameworks);

        Assert.Equal("Half is covered.", answer.Summary);
        Assert.Equal(new Dictionary<string, string> { [Cra] = "Some.", [Iso] = "Most." }, answer.Texts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Nothing in the shape")]
    [InlineData("SUMMARY:   \nTEXT : x\nTEXT CRA x")]
    public void An_answer_of_another_shape_gives_nothing(string answer)
    {
        var parsed = StatusReportPrompt.Parse(answer, [Framework(Cra, 2, 1)]);

        Assert.Equal((null, 0), (parsed.Summary, parsed.Texts.Count));
    }

    [Fact]
    public void A_long_answer_is_cut_to_the_longest_text()
    {
        var parsed = StatusReportPrompt.Parse("SUMMARY: " + new string('x', 2000), [Framework(Cra, 2, 1)]);

        Assert.Equal(StatusText.MaxLength, parsed.Summary!.Length);
    }

    [Fact]
    public void The_prompt_holds_the_figures_and_names_every_regulation()
    {
        var prompt = StatusReportPrompt.For("Acme Portal", [Framework(Cra, 2, 1), Framework(Iso, 2, 1)], new ManagementFacts(2, 0, 1, null));

        Assert.Contains(StatusReportPrompt.Marker, prompt);
        Assert.Contains("CRA version 1: 1 of 2 requirements covered", prompt);
        Assert.Contains("Open High and Critical risks: 2. Open severe scan findings: 0. Open security events: 1.", prompt);
        Assert.Contains("for each of: CRA, ISO/IEC 27001:2022", prompt);
    }

    // ---------------------------------------------------------- Shown and locked (R2, R4, R5)

    [Fact]
    public void The_summary_comes_first_and_texts_of_regulations_no_longer_followed_are_left_out()
    {
        var cra = Framework(Cra, 2, 1);
        var basis = StatusReportData.BasisOf(Cra, [cra], NoFacts)!;
        var texts = new[] { Drafted(Cra, basis), Drafted(Iso), Drafted(StatusText.Summary) };

        var lines = StatusReportService.Lines(texts, [cra], NoFacts);

        Assert.Equal(["Summary", "CRA"], lines.Select(l => l.Label));
        Assert.Equal((true, false), (lines[0].IsOutdated, lines[1].IsOutdated));
    }

    [Fact]
    public void An_AI_text_blocks_the_lock_before_an_outdated_one()
    {
        var aiText = new StatusTextLine(Guid.NewGuid(), "summary", "Summary", "t", true, true, "m", Monday);
        var outdated = aiText with { IsAiGenerated = false };
        var fine = outdated with { IsOutdated = false };

        Assert.Equal(StatusReportService.NotAccepted, StatusReportService.Blocker([fine, aiText]));
        Assert.Equal(StatusReportService.Outdated, StatusReportService.Blocker([fine, outdated]));
        Assert.Null(StatusReportService.Blocker([fine]));
        Assert.Null(StatusReportService.Blocker([]));
    }

    [Fact]
    public void The_report_marks_an_AI_text_and_an_outdated_one()
    {
        var line = new StatusTextLine(Guid.NewGuid(), "summary", "Summary", "Half is covered.", true, true, "m", Monday);

        Assert.Equal("Summary (AI-generated) (outdated: the figures changed): Half is covered.", ManagementPriorities.TextRow(line));
        Assert.Equal("CRA: Fine.", ManagementPriorities.TextRow(line with { Label = "CRA", Text = "Fine.", IsAiGenerated = false, IsOutdated = false }));
    }

    [Fact]
    public void The_texts_come_right_after_the_status_and_only_when_there_are_some()
    {
        var cra = Framework(Cra, 2, 1);
        var line = new StatusTextLine(Guid.NewGuid(), "summary", "Summary", "Half.", false, false, "m", Monday);

        var without = ManagementPriorities.Sheets([cra], NoFacts);
        var with = ManagementPriorities.Sheets([cra], NoFacts with { Texts = [line] });

        Assert.DoesNotContain(without, s => s.Name == ManagementPriorities.Assessment);
        Assert.Equal(("Status", ManagementPriorities.Assessment, without.Count + 1), (with[0].Name, with[1].Name, with.Count));
    }
}
