using System.Text.Encodings.Web;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ComplianceTracker.Web.TagHelpers
{
    // US-173 R1–R3: <term name="Residual risk">Residual</term> shows the text with a small "?" after it. The "?" opens a
    // one-line explanation (a Bootstrap popover, site.js) and, without JavaScript, is a link to the glossary entry.
    // An unknown term is shown as plain text, so a page never breaks because of a typo.
    [HtmlTargetElement("term")]
    public class TermTagHelper : TagHelper
    {
        public const string GlossaryPath = "/Help";

        public string Name { get; set; } = "";

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var text = (await output.GetChildContentAsync()).GetContent();
            var found = Glossary.Find(Name);
            output.TagName = "span";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.Attributes.SetAttribute("class", "term");
            if (found is null)
            {
                output.Content.SetHtmlContent(text);
                return;
            }

            var e = HtmlEncoder.Default;
            var explanation = $"{found.Explanation} ({found.Source})";
            output.Content.SetHtmlContent(
                $"{(string.IsNullOrWhiteSpace(text) ? e.Encode(found.Term) : text)}"
                + $"<a class=\"term-help\" href=\"{GlossaryPath}#{found.Anchor}\" data-term=\"{e.Encode(found.Term)}\" data-explanation=\"{e.Encode(explanation)}\""
                + $" title=\"{e.Encode(explanation)}\" aria-label=\"What is {e.Encode(found.Term)}?\">?</a>");
        }
    }
}
