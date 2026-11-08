using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.UnitTests.Frameworks;

// Edge-case unit tests of US-070 (and the US-007 extraction it changes): what the parser finds and what it leaves.
// Arrange / Act / Assert.
public class RequirementParserTests
{
    private static ExtractionResult Parse(params string[][] pages) => RequirementParser.Parse(pages);

    private static IReadOnlyList<string> Codes(ExtractionResult result) => result.Requirements.Select(r => r.Code).ToList();

    [Fact]
    public void A_standard_is_read_as_before()
    {
        // Act
        var result = Parse(["A.5.15 Access control", "Rules shall be set.", "A.5.16 Identity management"]);

        // Assert
        Assert.Equal(["A.5.15", "A.5.16"], Codes(result));
        Assert.Equal("Rules shall be set.", result.Requirements[0].Description);
        Assert.Empty(result.Check.LeftOver);
        Assert.Equal(100, result.Check.CoveredPercent);
    }

    [Fact]
    public void No_pages_give_an_empty_check_without_a_warning()
    {
        // Act
        var result = Parse();

        // Assert
        Assert.Empty(result.Requirements);
        Assert.Equal(0, result.Check.PagesWithText);
        Assert.False(result.Check.NeedsWarning);
        Assert.Equal(100, result.Check.CoveredPercent);
    }

    [Fact]
    public void Text_before_the_first_requirement_is_left_over()
    {
        // Act
        var result = Parse(["Introduction to the policy", "5.1 Access control"]);

        // Assert
        Assert.Equal([new LeftOverText(1, "Introduction to the policy")], result.Check.LeftOver);
        Assert.InRange(result.Check.CoveredPercent, 1, 99);
    }

    [Fact]
    public void A_page_without_a_requirement_start_is_counted_and_listed()
    {
        // Act
        var result = Parse(["5.1 Access control"], ["Records are kept for five years."]);

        // Assert
        Assert.Equal(1, result.Check.PagesWithoutRequirements);
        Assert.Equal(2, result.Check.PagesWithText);
        Assert.Contains(result.Check.LeftOver, l => l.Page == 2);
    }

    [Theory]
    [InlineData(5, 1, false)] // 20 %: not above the limit
    [InlineData(4, 1, true)] // 25 %
    public void The_warning_starts_above_20_percent(int pages, int withoutRequirements, bool warns)
    {
        // Arrange
        var all = Enumerable.Range(1, pages)
            .Select(p => p <= withoutRequirements ? new[] { $"Background text {p}" } : new[] { $"5.{p} Rule {p}" })
            .ToArray();

        // Act
        var check = Parse(all).Check;

        // Assert
        Assert.Equal(warns, check.NeedsWarning);
    }

    [Fact]
    public void Page_numbers_headers_and_footers_are_not_listed()
    {
        // Act
        var result = Parse(
            ["Acme policy", "5.1 Access control", "1"],
            ["Acme policy", "5.2 Passwords", "Page 2 of 3"],
            ["Acme policy", "5.3 Backups", "- 3 -"]);

        // Assert
        Assert.Empty(result.Check.LeftOver);
        Assert.Equal(["5.1", "5.2", "5.3"], Codes(result));
    }

    [Fact]
    public void A_header_needs_three_pages_to_be_recognised()
    {
        // Act: with two pages a repeated line is still text
        var result = Parse(["Acme policy", "5.1 Access control"], ["Acme policy", "5.2 Passwords"]);

        // Assert
        Assert.Contains(result.Check.LeftOver, l => l.Text == "Acme policy");
    }

    [Fact]
    public void A_gap_is_found_in_its_own_section_only()
    {
        // Act
        var gaps = RequirementParser.Gaps(["5.14", "5.16", "6.1", "6.2"]);

        // Assert
        Assert.Equal(["5.15 may be missing (5.14 is followed by 5.16)"], gaps);
    }

    [Fact]
    public void Gaps_are_found_at_sub_levels_and_in_annex_codes()
    {
        // Act
        var gaps = RequirementParser.Gaps(["A.5.1", "A.5.3", "5.1.1", "5.1.3"]);

        // Assert
        Assert.Contains("A.5.2 may be missing (A.5.1 is followed by A.5.3)", gaps);
        Assert.Contains("5.1.2 may be missing (5.1.1 is followed by 5.1.3)", gaps);
    }

    [Fact]
    public void Gaps_between_articles_are_found_but_not_inside_paragraphs()
    {
        // Act
        var gaps = RequirementParser.Gaps(["Art. 13", "Art. 15", "Art. 16(1)", "Art. 16(3)"]);

        // Assert
        Assert.Equal(["Art. 14 may be missing (Art. 13 is followed by Art. 15)"], gaps);
    }

    [Fact]
    public void A_long_gap_is_capped()
    {
        // Act
        var gaps = RequirementParser.Gaps(["5.1", "5.999"]);

        // Assert
        Assert.Equal(200, gaps.Count);
    }

    [Fact]
    public void A_contents_entry_that_is_found_is_not_listed_and_one_that_is_not_is()
    {
        // Act
        var result = Parse(["5.1 Access control ....... 2", "5.2 Passwords ....... 3"], ["5.1 Access control", "Granted by role."]);

        // Assert
        Assert.Equal(["5.2"], result.Check.NotFoundFromContents);
        Assert.Equal(["5.1"], Codes(result));
    }

    [Fact]
    public void A_contents_line_without_page_number_is_still_contents()
    {
        // Act
        var result = Parse(["5.1 Access control ........", "5.1 Access control", "Granted by role."]);

        // Assert
        Assert.Empty(result.Check.NotFoundFromContents);
        Assert.Empty(result.Check.LeftOver);
    }

    [Fact]
    public void Shall_must_and_required_sentences_outside_requirements_are_found()
    {
        // Act
        var result = Parse(["Suppliers shall encrypt backups. Logs must be kept. Staff are required to train. Coffee is free."]);

        // Assert
        Assert.Equal(3, result.Check.ShallSentences.Count);
        Assert.DoesNotContain(result.Check.ShallSentences, s => s.Text.Contains("Coffee", StringComparison.Ordinal));
    }

    [Fact]
    public void A_shall_sentence_inside_a_requirement_is_not_listed()
    {
        // Act
        var result = Parse(["5.1 Access control", "Access shall be granted by role."]);

        // Assert
        Assert.Empty(result.Check.ShallSentences);
    }

    [Fact]
    public void A_long_left_over_text_is_cut_to_300_characters()
    {
        // Act
        var result = Parse([new string('x', 50) + " " + new string('y', 400)]);

        // Assert
        Assert.Equal(ExtractionCheck.MaxTextLength, result.Check.LeftOver.Single().Text.Length);
        Assert.EndsWith("…", result.Check.LeftOver.Single().Text);
    }

    [Fact]
    public void An_EU_act_is_split_down_to_its_points()
    {
        // Act
        var result = Parse(
            ["(1) Whereas products shall be secure.", "HAVE ADOPTED THIS REGULATION:"],
            ["Article 13", "Obligations of manufacturers", "1. Manufacturers shall ensure security.", "2. Manufacturers shall assess risks:", "(a) in planning;", "(b) in design."],
            ["Article 14", "Reporting", "Manufacturers shall notify."]);

        // Assert
        Assert.Equal(["Art. 13(1)", "Art. 13(2)(a)", "Art. 13(2)(b)", "Art. 14"], Codes(result));
        Assert.Equal("Reporting", result.Requirements[^1].Title);
        Assert.Empty(result.Check.ShallSentences);
    }

    [Fact]
    public void An_annex_is_split_into_parts_and_numbered_items()
    {
        // Act
        var result = Parse(
            ["Article 1", "Subject matter"],
            ["ANNEX I", "ESSENTIAL REQUIREMENTS", "Part I", "(1) Products shall be secure.", "(2) Products shall:", "(a) have no known vulnerabilities;", "Part II", "(1) Identify components."]);

        // Assert
        Assert.Contains("Annex I Part I (1)", Codes(result));
        Assert.Contains("Annex I Part I (2)(a)", Codes(result));
        Assert.Contains("Annex I Part II (1)", Codes(result));
        Assert.DoesNotContain("Annex I Part I (2)", Codes(result));
        Assert.DoesNotContain("Annex I", Codes(result));
    }

    [Fact]
    public void Numbered_lines_in_a_standard_are_not_mistaken_for_paragraphs()
    {
        // Act: no "Article" anywhere, so "1." is just text
        var result = Parse(["5.1 Access control", "1. Keep a list of users.", "(a) admins"]);

        // Assert
        Assert.Equal(["5.1"], Codes(result));
    }

    [Fact]
    public void Article_in_running_text_is_not_a_heading()
    {
        // Act
        var result = Parse(["Article 1", "Scope", "This applies as set out in Article 14 of the act."]);

        // Assert
        Assert.Equal(["Art. 1"], Codes(result));
    }

    [Fact]
    public void A_code_found_twice_is_kept_once_and_reported()
    {
        // Act
        var result = Parse(["5.1 Access control", "5.1 Access control", "The real text."]);

        // Assert
        Assert.Equal(["5.1"], Codes(result));
        Assert.Equal("The real text.", result.Requirements.Single().Description);
        Assert.Equal(["5.1"], result.Check.FoundTwice);
    }

    [Fact]
    public void The_summary_is_written_for_the_log()
    {
        // Act
        var check = Parse(["5.1 Access control"], ["Background", "Suppliers shall encrypt."]).Check;

        // Assert
        Assert.Equal("1 requirement, 1 page without requirements, 1 \"shall\" sentence outside requirements, warning: 50 % of the pages gave no requirements", check.Summary);
    }

    // ---------------------------------------------------------- R6: requirements added by hand

    [Fact]
    public void A_requirement_added_by_hand_removes_the_text_it_holds()
    {
        // Arrange
        var check = Parse(["5.1 Access control"], ["Suppliers shall encrypt backups."]).Check;

        // Act
        var left = check.Without([("5.1", "Access control", null), ("5.2", "Encryption", "Suppliers shall encrypt backups.")]);

        // Assert
        Assert.Empty(left.LeftOver);
        Assert.Empty(left.ShallSentences);
    }

    [Fact]
    public void The_extracted_requirements_do_not_hide_the_text_they_swallowed()
    {
        // Arrange: the text on page 2 was added to 5.1's description
        var check = Parse(["5.1 Access control"], ["Suppliers shall encrypt backups."]).Check;

        // Act
        var left = check.Without([("5.1", "Access control", "Suppliers shall encrypt backups.")]);

        // Assert
        Assert.Single(left.LeftOver);
    }

    [Fact]
    public void A_requirement_added_by_hand_closes_a_gap_and_a_contents_entry()
    {
        // Arrange
        var check = Parse(["5.1 Access ....... 1", "5.2 Passwords ....... 2", "5.3 Backups ...... 3"], ["5.1 Access", "5.3 Backups"]).Check;

        // Act
        var left = check.Without([("5.1", "Access", null), ("5.3", "Backups", null), ("5.2", "Passwords", null)]);

        // Assert
        Assert.NotEmpty(check.PossiblyMissing);
        Assert.Empty(left.PossiblyMissing);
        Assert.Empty(left.NotFoundFromContents);
    }

    [Fact]
    public void A_check_from_before_the_codes_were_kept_still_filters()
    {
        // Arrange: Codes is null for a check stored without it
        var check = new ExtractionCheck(1, 1, 0, 90, [new LeftOverText(1, "Suppliers shall encrypt.")], [], [], [], []);

        // Act
        var left = check.Without([("5.2", "Encryption", "Suppliers shall encrypt.")]);

        // Assert
        Assert.Empty(left.LeftOver);
    }
}
