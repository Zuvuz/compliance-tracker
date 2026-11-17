using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ComplianceTracker.Web.TagHelpers
{
    // US-172 R1–R3: an empty list says what belongs there and why (one sentence), and offers the first step(s) the member
    // may take (child content). A member who may not take them sees who can (cannot-text). The list's id stays on it.
    //   <empty-state id="no-risks" title="No risks yet" why="The CRA asks for a risk assessment of the product."
    //                can-act="@canManage" cannot-text="Someone who manages risks can add them."> …actions… </empty-state>
    [HtmlTargetElement("empty-state")]
    public class EmptyStateTagHelper : TagHelper
    {
        public string Title { get; set; } = "";
        public string? Why { get; set; }
        public bool CanAct { get; set; } = true;
        public string? CannotText { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var actions = (await output.GetChildContentAsync()).GetContent();
            var e = HtmlEncoder.Default;

            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            var extra = output.Attributes.TryGetAttribute("class", out var existing) ? " " + existing.Value : "";
            output.Attributes.SetAttribute("class", "empty-state border rounded p-3 mb-3" + extra);

            var html = new System.Text.StringBuilder();
            // Each part on its own line, so the page's text reads as separate lines
            html.Append("<p class=\"fw-semibold mb-1 empty-state-title\">").Append(e.Encode(Title)).Append("</p>").Append('\n');
            if (!string.IsNullOrWhiteSpace(Why))
            {
                html.Append("<p class=\"small text-muted mb-2 empty-state-why\">").Append(e.Encode(Why)).Append("</p>").Append('\n');
            }

            if (CanAct && !string.IsNullOrWhiteSpace(actions))
            {
                html.Append("<div class=\"d-flex flex-wrap gap-2 empty-state-actions\">").Append(actions).Append("</div>").Append('\n');
            }
            else if (!CanAct && !string.IsNullOrWhiteSpace(CannotText))
            {
                html.Append("<p class=\"small text-muted mb-0 empty-state-cannot\">").Append(e.Encode(CannotText)).Append("</p>").Append('\n');
            }

            output.Content.SetHtmlContent(html.ToString());
        }
    }
}
