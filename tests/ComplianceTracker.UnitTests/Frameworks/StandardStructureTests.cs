using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.UnitTests.Frameworks;

// Edge-case unit tests of US-143: a standard's structure, kinds and what is left out, and tables with chosen columns.
// The lines are made up and copy only the layout of ISO standards (R9). Arrange / Act / Assert.
public class StandardStructureTests
{
    private const string Watermark = "Licensed to Acme ApS – single user licence only";

    private static ExtractionResult Parse(params string[][] pages) => RequirementParser.Parse(pages);

    private static ExtractedRequirement Get(ExtractionResult result, string code)
        => result.Requirements.SingleOrDefault(r => r.Code == code) ?? throw new Xunit.Sdk.XunitException($"No '{code}' in {string.Join(", ", result.Requirements.Select(r => r.Code))}");

    // ---------------------------------------------------------- R4: the tree

    [Fact]
    public void Lettered_points_sit_under_their_clause()
    {
        // Act
        var result = Parse(["6.1.2 Risk assessment", "The organization shall define a process that:", "a) sets criteria;", "b) finds risks."]);

        // Assert
        Assert.Equal(["6.1.2", "6.1.2 a)", "6.1.2 b)"], result.Requirements.Select(r => r.Code));
        Assert.Equal("6.1.2", Get(result, "6.1.2 b)").ParentCode);
        Assert.Null(Get(result, "6.1.2").ParentCode);
    }

    [Fact]
    public void A_clause_at_the_bottom_of_a_page_keeps_its_text_from_the_next_page()
    {
        // Act
        var result = Parse(["4.1 Understanding the context"], ["The organization shall work out what matters."]);

        // Assert
        Assert.Equal("The organization shall work out what matters.", Get(result, "4.1").Description);
    }

    [Fact]
    public void An_annex_control_sits_under_the_annex_also_when_its_table_runs_over_two_pages()
    {
        // Arrange: the header row "Control  Name" is repeated on every page, like a table that runs on
        string[] Page(params string[] lines) => ["Control Name", .. lines];

        // Act
        var result = Parse(
            Page("Annex A (normative)", "Controls reference", "These controls shall be compared.", "A.5.1 Policies", "Policies shall be set."),
            Page("Rules shall be followed on every site.", "A.5.2 Roles", "Roles shall be given."),
            Page("A.5.3 Duties", "Duties shall be split."));

        // Assert
        Assert.Equal("Policies shall be set. Rules shall be followed on every site.", Get(result, "A.5.1").Description);
        Assert.All(new[] { "A.5.1", "A.5.2", "A.5.3" }, code => Assert.Equal("Annex A", Get(result, code).ParentCode));
        Assert.Equal("Controls reference", Get(result, "Annex A").Title);
    }

    [Fact]
    public void A_heading_with_nothing_of_its_own_is_left_out_and_what_was_under_it_moves_up()
    {
        // Act
        var result = Parse(["6 Planning", "6.1 Actions", "6.1.1 General", "The organization shall plan."]);

        // Assert
        Assert.Equal(["6.1.1"], result.Requirements.Select(r => r.Code));
        Assert.Null(Get(result, "6.1.1").ParentCode);
    }

    [Fact]
    public void A_code_twice_is_kept_once_with_the_most_text_and_listed()
    {
        // Act
        var result = Parse(["5.1 Policies"], ["5.1 Policies", "Policies shall be set."]);

        // Assert
        Assert.Equal("Policies shall be set.", Get(result, "5.1").Description);
        Assert.Contains("5.1", result.Check.FoundTwice);
    }

    [Theory]
    [InlineData("6.1.2 a)", "6.1.2")]
    [InlineData("6.1.2", "6.1")]
    [InlineData("A.5.1", "Annex A")]
    [InlineData("4.1", null)]
    [InlineData("Annex A", null)]
    [InlineData("Art. 14(2)", null)]
    public void The_parent_is_the_nearest_code_above_that_exists(string code, string? expected)
    {
        // Arrange
        var codes = new HashSet<string>(["6.1.2", "6.1", "Annex A"], StringComparer.OrdinalIgnoreCase);

        // Act / Assert
        Assert.Equal(expected, RequirementParser.ParentCode(code, codes));
    }

    // ---------------------------------------------------------- R3: what is left out

    [Fact]
    public void A_watermark_on_every_page_is_never_a_requirement()
    {
        // Act
        var result = Parse([Watermark, "4.1 Context", "The organization shall look."], [Watermark, "4.2 Parties", "Parties shall be listed."], [Watermark, "© ISO 2022 – All rights reserved"]);

        // Assert
        Assert.DoesNotContain(result.Requirements, r => r.Description.Contains("Licensed") || r.Description.Contains("©"));
        Assert.Equal(["4.1", "4.2"], result.Requirements.Select(r => r.Code));
    }

    [Fact]
    public void Foreword_introduction_scope_and_terms_are_left_out()
    {
        // Act
        var result = Parse([
            "Foreword", "Made-up text.", "Introduction", "0.1 General", "Text that shall not count.",
            "1 Scope", "This document shall cover tests.", "2 Normative references", "None.",
            "3 Terms and definitions", "3.1 Test term", "a word", "4 Context of the organization", "4.1 Understanding", "The organization shall look."]);

        // Assert
        Assert.Equal(["4.1"], result.Requirements.Select(r => r.Code));
        Assert.Empty(result.Check.LeftOver);
    }

    [Fact]
    public void Notes_and_examples_are_left_out_until_the_next_point()
    {
        // Act
        var result = Parse(["5.1 Policies", "Policies shall be set.", "NOTE 1 Policies can be short.", "They can also be long.",
            "EXAMPLE A policy on passwords.", "5.2 Roles", "Roles shall be given."]);

        // Assert
        Assert.Equal("Policies shall be set.", Get(result, "5.1").Description);
        Assert.Equal("Roles shall be given.", Get(result, "5.2").Description);
    }

    [Fact]
    public void An_informative_annex_and_the_bibliography_are_left_out_but_a_normative_annex_after_them_is_read()
    {
        // Act
        var result = Parse(["4.1 Context", "The organization shall look.", "Annex B (informative)", "B.1 Guidance", "Guidance shall help.",
            "Annex C (normative)", "Rules", "These shall apply.", "C.1 Rule", "A rule shall apply.", "Bibliography", "[1] A book", "D.1 Not read"]);

        // Assert
        Assert.Equal(["4.1", "Annex C", "C.1"], result.Requirements.Select(r => r.Code));
    }

    // ---------------------------------------------------------- R5: the kind

    [Theory]
    [InlineData("Policies shall be set.", false, RequirementKind.Requirement)]
    [InlineData("Passwords shall not be shared.", false, RequirementKind.Requirement)]
    [InlineData("A policy should be defined.", false, RequirementKind.Recommendation)]
    [InlineData("It shall be set and should be short.", false, RequirementKind.Requirement)]
    [InlineData("Degree to which it can be changed.", true, RequirementKind.Characteristic)]
    [InlineData("Degree to which it can be changed.", false, RequirementKind.Requirement)]
    [InlineData("Rules should be followed.", true, RequirementKind.Recommendation)]
    [InlineData("SHALL in capitals", false, RequirementKind.Requirement)]
    [InlineData("marshall the troops", false, RequirementKind.Requirement)]
    [InlineData("shoulder the load", true, RequirementKind.Characteristic)]
    public void The_kind_comes_from_shall_should_or_the_quality_model(string text, bool qualityModel, RequirementKind expected)
    {
        // Act / Assert
        Assert.Equal(expected, RequirementParser.Classify(text, qualityModel));
    }

    [Fact]
    public void A_quality_model_gives_characteristics_under_characteristics()
    {
        // Act
        var result = Parse(["The product quality model has characteristics.", "4.1 Maintainability", "How easily it can be changed.",
            "4.1.5 Testability", "How easily it can be tested."]);

        // Assert
        Assert.Equal(RequirementKind.Characteristic, Get(result, "4.1.5").Kind);
        Assert.Equal("4.1", Get(result, "4.1.5").ParentCode);
    }

    [Fact]
    public void An_eu_act_keeps_its_requirements_flat_and_as_requirements()
    {
        // Act
        var result = Parse(["Article 13", "Obligations", "1. The manufacturer should consider.", "(a) a point"]);

        // Assert
        Assert.All(result.Requirements, r => Assert.Equal((RequirementKind.Requirement, (string?)null), (r.Kind, r.ParentCode)));
    }

    // ---------------------------------------------------------- R7: tables

    private static readonly IReadOnlyList<IReadOnlyList<string>> Soa =
    [
        ["Control", "Name", "Description", "Parent", "Type"],
        ["A.5", "Organizational controls", "Controls for the organization", "", ""],
        ["A.5.1", "Policies", "Policies should be set", "A.5", ""],
        ["", "", "", "", ""],
        ["A.5.2", "Roles", "Roles shall be given", "A.5", "Recommendation"],
        ["A.5.1", "Policies again", "Twice", "", ""],
        ["A.5.3", "", "No title", "", ""]
    ];

    [Fact]
    public void A_table_is_read_with_the_chosen_columns()
    {
        // Act
        var result = TableParser.Parse(Soa, new TableColumns("Control", "Name", "Description", "Parent", "Type"));

        // Assert
        Assert.Equal(["A.5", "A.5.1", "A.5.2", "A.5.3"], result.Requirements.Select(r => r.Code));
        Assert.Equal("A.5", result.Requirements[1].ParentCode);
        Assert.Equal(RequirementKind.Recommendation, result.Requirements[1].Kind); // from the text
        Assert.Equal(RequirementKind.Recommendation, result.Requirements[2].Kind); // the kind column wins over "shall"
        Assert.Equal("A.5.3", result.Requirements[3].Title); // no title: the code
        Assert.Equal(["A.5.1"], result.Check.FoundTwice);
    }

    [Fact]
    public void Headers_are_matched_ignoring_case_and_spaces()
    {
        // Act
        var result = TableParser.Parse([[" control ", "NAME", "description"], ["A.5.1", "Policies", "Text"]], new TableColumns("Control", "Name", "Description"));

        // Assert
        Assert.Single(result.Requirements);
    }

    [Fact]
    public void A_missing_column_is_named()
    {
        // Act
        var error = Assert.Throws<DomainException>(() => TableParser.Parse(Soa, new TableColumns("Control", "Title", "Description")));

        // Assert
        Assert.Equal("The column \"Title\" is not in the file", error.Message);
    }

    [Fact]
    public void A_short_row_and_an_empty_table_do_not_fail()
    {
        // Act
        var shortRow = TableParser.Parse([["Control", "Name", "Description"], ["A.5.1"]], new TableColumns("Control", "Name", "Description"));
        var empty = TableParser.Parse([], new TableColumns("Control", "Name", "Description"));

        // Assert
        Assert.Equal(("A.5.1", ""), (shortRow.Requirements[0].Title, shortRow.Requirements[0].Description));
        Assert.Empty(empty.Requirements);
    }

    [Fact]
    public void The_column_choice_survives_being_remembered()
    {
        // Arrange
        var columns = new TableColumns("Control", "Name", "Description", null, "Type");

        // Act
        var back = TableColumns.Parse(columns.Serialize());

        // Assert
        Assert.Equal(columns, back);
        Assert.Null(TableColumns.Parse(null));
        Assert.Null(TableColumns.Parse("only one part"));
    }

    [Fact]
    public void Choosing_needs_three_columns_of_the_file()
    {
        // Arrange
        string[] headers = ["Control", "Name", "Description"];

        // Act / Assert
        Assert.Equal(new TableColumns("Control", "Name", "Description"), TableColumns.Choose(headers, "control", "name", "description", "", "Nope"));
        Assert.Throws<DomainException>(() => TableColumns.Choose(headers, "Control", "Name", null, null, null));
    }
}
