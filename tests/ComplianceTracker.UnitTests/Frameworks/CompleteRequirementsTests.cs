using System.IO.Compression;
using System.Text;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Infrastructure.Frameworks;

namespace ComplianceTracker.UnitTests.Frameworks;

// Edge-case unit tests of US-137: the Formex reader, the profiles, the CELEX number and the EUR-Lex package.
// The Formex files are written by hand in the official structure. Arrange / Act / Assert.
public class CompleteRequirementsTests
{
    private static FormexParser.Result Parse(params string[] files) => FormexParser.Parse("EUR-Lex test", files);

    private static IReadOnlyList<string> Codes(FormexParser.Result result) => result.Extraction.Requirements.Select(r => r.Code).ToList();

    private static string Act(string articles, string recitals = "") => $"<ACT><PREAMBLE><GR.CONSID>{recitals}</GR.CONSID></PREAMBLE><ENACTING.TERMS>{articles}</ENACTING.TERMS></ACT>";

    private static string Article(string number, string title, string body) => $"<ARTICLE><TI.ART>Article {number}</TI.ART><STI.ART>{title}</STI.ART>{body}</ARTICLE>";

    private static string Parag(string number, string text) => $"<PARAG><NO.PARAG>{number}.</NO.PARAG><ALINEA>{text}</ALINEA></PARAG>";

    private static string List(params (string Label, string Text)[] items)
        => "<LIST>" + string.Concat(items.Select(i => $"<ITEM><NP><NO.P>({i.Label})</NO.P><TXT>{i.Text}</TXT></NP></ITEM>")) + "</LIST>";

    // ---------------------------------------------------------- R1, R2: structure

    [Fact]
    public void A_paragraph_with_points_becomes_its_points_and_one_without_stays_whole()
    {
        // Act
        var result = Parse(Act(Article("13", "Obligations",
            Parag("1", "Manufacturers shall comply.") + Parag("2", "Manufacturers shall:" + List(("a", "assess risks;"), ("b", "document them."))))));

        // Assert
        Assert.Equal(["Art. 13(1)", "Art. 13(2)(a)", "Art. 13(2)(b)"], Codes(result));
        Assert.Equal("assess risks;", result.Extraction.Requirements[1].Description);
        Assert.Equal((1, 2, 2), (result.Completeness.Articles, result.Completeness.Paragraphs, result.Completeness.Points));
    }

    [Fact]
    public void Sub_points_are_split_too()
    {
        // Arrange
        var subList = "<LIST><ITEM><NP><NO.P>(i)</NO.P><TXT>first;</TXT></NP></ITEM><ITEM><NP><NO.P>(ii)</NO.P><TXT>second.</TXT></NP></ITEM></LIST>";
        var body = Parag("2", $"They shall:<LIST><ITEM><NP><NO.P>(a)</NO.P><TXT>do this:</TXT>{subList}</NP></ITEM></LIST>");

        // Act
        var result = Parse(Act(Article("14", "Reporting", body)));

        // Assert
        Assert.Equal(["Art. 14(2)(a)(i)", "Art. 14(2)(a)(ii)"], Codes(result));
    }

    [Fact]
    public void An_article_without_paragraphs_is_one_requirement_named_by_its_title()
    {
        // Act
        var result = Parse(Act(Article("5a", "Security updates", "<ALINEA>Manufacturers shall provide updates.</ALINEA>")));

        // Assert
        var requirement = Assert.Single(result.Extraction.Requirements);
        Assert.Equal(("Art. 5a", "Security updates"), (requirement.Code, requirement.Title));
    }

    [Fact]
    public void Annexes_keep_their_parts_and_come_in_order_whatever_the_file_order()
    {
        // Arrange
        string Annex(string number, string body) => $"<ANNEX><TITLE><TI><P>ANNEX {number}</P></TI></TITLE><CONTENTS>{body}</CONTENTS></ANNEX>";
        var annexII = Annex("II", "<NP><NO.P>1.</NO.P><TXT>Contact details shall be given.</TXT></NP>");
        var annexI = Annex("I", "<GR.SEQ><TITLE><TI><P>Part II</P></TI></TITLE>" + List(("1", "Handle vulnerabilities."), ("2", "Test them.")) + "</GR.SEQ>");

        // Act
        var result = Parse(annexII, annexI);

        // Assert
        Assert.Equal(["Annex I Part II (1)", "Annex I Part II (2)", "Annex II (1)"], Codes(result));
        Assert.Equal(2, result.Completeness.Annexes);
    }

    [Fact]
    public void An_annex_without_numbered_parts_is_one_requirement()
    {
        // Act
        var result = Parse("<ANNEX><TITLE><TI><P>ANNEX III</P></TI><STI><P>Important products</P></STI></TITLE><CONTENTS><P>Identity management systems.</P></CONTENTS></ANNEX>");

        // Assert
        var requirement = Assert.Single(result.Extraction.Requirements);
        Assert.Equal(("Annex III", "Important products"), (requirement.Code, requirement.Title));
    }

    // ---------------------------------------------------------- R3: left out with a reason

    [Fact]
    public void Recitals_definitions_authorities_deleted_and_final_provisions_are_left_out_with_their_reason()
    {
        // Arrange
        var recitals = "<CONSID><NP><NO.P>(1)</NO.P><TXT>Why.</TXT></NP></CONSID><CONSID><NP><NO.P>(2)</NO.P><TXT>Why too.</TXT></NP></CONSID>";
        var act = Act(
            Article("3", "Definitions", "<ALINEA>In this Regulation:" + List(("1", "'product' means …"), ("2", "'manufacturer' means …")) + "</ALINEA>")
            + Article("46", "Market surveillance", Parag("1", "Member States shall designate authorities.") + Parag("2", "The Commission may adopt acts.")
                + Parag("3", "Manufacturers shall cooperate."))
            + Article("13", "Obligations", Parag("4", "(deleted)"))
            + Article("71", "Entry into force and application", Parag("1", "It enters into force.")),
            recitals);

        // Act
        var result = Parse(act);

        // Assert
        Assert.Equal(["Art. 46(3)"], Codes(result));
        Assert.Equal(
            [("Recital (1)", "Recital"), ("Recital (2)", "Recital"), ("Art. 3(1)", "Definition"), ("Art. 3(2)", "Definition"),
             ("Art. 46(1)", "An authority's duty"), ("Art. 46(2)", "An authority's duty"), ("Art. 13(4)", "Deleted"), ("Art. 71(1)", "Final provision")],
            result.Completeness.LeftOut.Select(p => (p.Code, p.Reason)));
    }

    [Theory]
    [InlineData("Member States shall lay down penalties.", true)]
    [InlineData("The Commission is empowered to adopt delegated acts.", true)]
    [InlineData("Notified bodies shall keep records.", true)]
    [InlineData("Manufacturers shall inform the market surveillance authorities.", false)]
    [InlineData("Where Member States so decide, manufacturers shall report.", false)]
    public void An_authoritys_duty_is_found_by_who_must_act(string text, bool leftOut)
    {
        // Act
        var result = Parse(Act(Article("9", "Duties", Parag("1", text))));

        // Assert
        Assert.Equal(leftOut, result.Completeness.LeftOut.Count == 1);
    }

    [Fact]
    public void A_code_twice_is_kept_once_and_listed()
    {
        // Act
        var result = Parse(Act(Article("5", "One", "<ALINEA>Shall.</ALINEA>")), Act(Article("5", "One again", "<ALINEA>Shall.</ALINEA>")));

        // Assert
        Assert.Equal(["Art. 5"], Codes(result));
        Assert.Equal(["Art. 5"], result.Extraction.Check.FoundTwice);
    }

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><ACT>&e;</ACT>")]
    [InlineData("")]
    public void A_file_that_is_not_safe_formex_gives_nothing_and_reaches_nothing(string xml)
    {
        // Act
        var result = Parse(xml);

        // Assert
        Assert.Empty(result.Extraction.Requirements);
    }

    // ---------------------------------------------------------- R4: profiles

    [Theory]
    [InlineData("ISO/IEC 27001:2022", "ISO/IEC 27001:2022")]
    [InlineData("iso 27001", "ISO/IEC 27001:2022")]
    [InlineData("DS/EN ISO/IEC 27002", "ISO/IEC 27002:2022")]
    [InlineData("ISO 270011", null)]
    [InlineData("Security policy", null)]
    public void The_profile_is_found_by_the_framework_name(string name, string? expected)
    {
        // Act / Assert
        Assert.Equal(expected, FrameworkProfiles.For(name)?.Name);
    }

    [Fact]
    public void The_27001_profile_has_30_clauses_and_93_controls()
    {
        // Act
        var groups = FrameworkProfiles.For("ISO/IEC 27001")!.Check([]);

        // Assert
        Assert.Equal([("clauses", 30), ("controls", 93)], groups.Select(g => (g.Name, g.Expected)));
    }

    [Fact]
    public void A_missing_control_and_one_that_is_not_in_the_standard_are_listed()
    {
        // Arrange
        var profile = FrameworkProfiles.For("ISO/IEC 27001")!;
        var codes = profile.Groups[1].Codes.Where(c => c != "A.5.23").Append("A.5.38").Append(" a.5.1 ").Append("6.1.2 a)");

        // Act
        var controls = profile.Check(codes)[1];

        // Assert
        Assert.Equal("92 of 93 controls found", controls.Text);
        Assert.Equal(["A.5.23"], controls.Missing);
        Assert.Equal(["A.5.38"], controls.Extra);
    }

    [Fact]
    public void The_summary_names_a_few_missing_codes_or_counts_many()
    {
        // Arrange
        var few = new CompletenessCheck("f.pdf", 0, 0, 0, 0, 92, [], "ISO/IEC 27001:2022", [new("controls", 93, 92, ["A.5.23"], [])]);
        var many = few with { Groups = [new("controls", 93, 80, Enumerable.Range(1, 13).Select(n => $"A.8.{n}").ToList(), [])] };

        // Act / Assert
        Assert.Equal("ISO/IEC 27001:2022: 92 of 93 controls found; A.5.23 is missing", few.Summary);
        Assert.EndsWith("13 codes are missing", many.Summary);
        Assert.True(few.HasGaps);
    }

    // ---------------------------------------------------------- R1: CELEX and the package

    [Theory]
    [InlineData("32024R2847", "32024R2847")]
    [InlineData(" 32016r0679 ", "32016R0679")]
    [InlineData("32022L2555", "32022L2555")]
    [InlineData("02016R0679-20160504", "02016R0679-20160504")]
    public void A_celex_number_is_cleaned(string celex, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, Celex.Check(celex));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2024/2847")]
    [InlineData("32024R2847; DROP TABLE")]
    [InlineData("../etc")]
    public void Anything_else_is_not_a_celex_number(string? celex)
    {
        // Act / Assert
        Assert.Equal("Give a CELEX number, e.g. 32024R2847 for the CRA", Assert.Throws<DomainException>(() => Celex.Check(celex)).Message);
    }

    [Fact]
    public void A_package_gives_its_xml_files_and_nothing_else()
    {
        // Arrange
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in new[] { ("act.fmx.xml", "<ACT/>"), ("annex.fmx.xml", "<ANNEX/>"), ("cover.pdf", "%PDF-") })
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(text);
            }
        }

        // Act
        var files = HttpEurLexSource.Unpack(stream.ToArray());

        // Assert
        Assert.Equal(["<ACT/>", "<ANNEX/>"], files);
    }

    [Fact]
    public void An_answer_that_is_not_a_package_is_refused()
    {
        // Act / Assert
        Assert.Equal("The answer from EUR-Lex is not a Formex package",
            Assert.Throws<DomainException>(() => HttpEurLexSource.Unpack(Encoding.UTF8.GetBytes("<html>Not found</html>"))).Message);
    }

    [Fact]
    public void A_package_without_xml_is_refused()
    {
        // Arrange
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("readme.txt");
        }

        // Act / Assert
        Assert.Throws<DomainException>(() => HttpEurLexSource.Unpack(stream.ToArray()));
    }
}
