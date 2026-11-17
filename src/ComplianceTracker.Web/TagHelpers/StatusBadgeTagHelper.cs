using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ComplianceTracker.Web.TagHelpers
{
    // US-168 R2: <status-badge status="Not covered" /> is a badge with the status's fixed colour. Other attributes (id,
    // class, title) are kept, so the tests' ids and classes stay as they are.
    [HtmlTargetElement("status-badge", TagStructure = TagStructure.NormalOrSelfClosing)]
    public class StatusBadgeTagHelper : TagHelper
    {
        public string? Status { get; set; }

        // The text, when it differs from the status, e.g. status "High" with the text "High risk"
        public string? Text { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "span";
            output.TagMode = TagMode.StartTagAndEndTag;
            var extra = output.Attributes.TryGetAttribute("class", out var existing) ? " " + existing.Value : "";
            output.Attributes.SetAttribute("class", StatusBadges.ClassOf(Status) + extra);
            output.Content.SetContent(Text ?? Status ?? "");
        }
    }
}
