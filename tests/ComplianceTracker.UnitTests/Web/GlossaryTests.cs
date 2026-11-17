using ComplianceTracker.Web.Models;
using ComplianceTracker.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ComplianceTracker.UnitTests.Web;

// US-173: edge cases of the glossary and the "?" after a term. Arrange / Act / Assert.
public class GlossaryTests
{
    [Fact]
    public void Every_term_has_a_short_explanation_and_a_source()
    {
        Assert.All(Glossary.Terms, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Source), t.Term);
            // Sentences end with ". " or at the end; "e.g." is not the end of one
            var sentences = System.Text.RegularExpressions.Regex.Split(t.Explanation.Replace("e.g.", "eg", StringComparison.Ordinal).TrimEnd('.'), @"\.\s+");
            Assert.InRange(sentences.Length, 1, 2);
        });
    }

    [Fact]
    public void Every_term_has_its_own_anchor()
        => Assert.Equal(Glossary.Terms.Count, Glossary.Terms.Select(t => t.Anchor).Distinct().Count());

    [Fact]
    public void A_term_is_found_whatever_the_case()
        => Assert.Equal("Residual risk", Glossary.Find("residual RISK")?.Term);

    private static async Task<string> RenderAsync(string name, string text)
    {
        var output = new TagHelperOutput("term", new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(text)));
        await new TermTagHelper { Name = name }.ProcessAsync(new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "x"), output);
        return output.Content.GetContent();
    }

    [Fact]
    public async Task A_known_term_gets_a_question_mark_that_links_to_the_glossary()
    {
        var html = await RenderAsync("Residual risk", "Residual");

        Assert.StartsWith("Residual<a class=\"term-help\" href=\"/Help#residual-risk\"", html);
        Assert.Contains("ISO/IEC 27001 6.1.3 f", html);
        Assert.Contains("aria-label=\"What is Residual risk?\"", html);
    }

    [Fact]
    public async Task An_unknown_term_is_plain_text()
        => Assert.Equal("Whatever", await RenderAsync("No such term", "Whatever"));

    [Fact]
    public async Task Without_text_the_term_itself_is_shown()
        => Assert.StartsWith("Coverage<a", await RenderAsync("Coverage", ""));
}
