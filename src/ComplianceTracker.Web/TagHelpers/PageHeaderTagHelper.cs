using System.Text.Encodings.Web;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ComplianceTracker.Web.TagHelpers
{
    // US-168 R1, R3: the shared page header: an optional way back, the page's title (h1) with its status, and the page's
    // actions on the right (child content): at most one primary button, the others outline, "More" for the rest.
    //   <page-header heading="@risk.Title" heading-id="risk-title" status="High" status-id="risk-level"
    //                back-href="…" back-text="Risks of Acme Portal"> …actions… </page-header>
    [HtmlTargetElement("page-header")]
    public class PageHeaderTagHelper : TagHelper
    {
        public string Heading { get; set; } = "";
        public string? HeadingId { get; set; }
        public string? Status { get; set; }
        public string? StatusId { get; set; }

        // The status text when it differs from the status, e.g. a phase
        public string? StatusText { get; set; }
        public string? BackHref { get; set; }
        public string? BackText { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var actions = (await output.GetChildContentAsync()).GetContent();
            var e = HtmlEncoder.Default;

            output.TagName = "header";
            output.TagMode = TagMode.StartTagAndEndTag;
            var extra = output.Attributes.TryGetAttribute("class", out var existing) ? " " + existing.Value : "";
            output.Attributes.SetAttribute("class", "page-header d-flex flex-wrap justify-content-between align-items-start gap-2 mb-2" + extra);

            var html = new System.Text.StringBuilder();
            html.Append("<div class=\"page-header-title\">");
            if (!string.IsNullOrEmpty(BackHref))
            {
                html.Append("<a class=\"page-back small d-inline-block mb-1\" href=\"").Append(e.Encode(BackHref)).Append("\">← ")
                    .Append(e.Encode(BackText ?? "Back")).Append("</a>");
            }

            html.Append("<h1 class=\"mb-1\"><span").Append(Id(HeadingId)).Append('>').Append(e.Encode(Heading)).Append("</span>");
            if (!string.IsNullOrEmpty(Status))
            {
                html.Append(" <span class=\"").Append(StatusBadges.ClassOf(Status)).Append(" fs-6 align-middle\"").Append(Id(StatusId)).Append('>')
                    .Append(e.Encode(StatusText ?? Status)).Append("</span>");
            }

            html.Append("</h1></div>");
            if (!string.IsNullOrWhiteSpace(actions))
            {
                html.Append("<div class=\"page-header-actions d-flex flex-wrap align-items-start gap-2\">").Append(actions).Append("</div>");
            }

            output.Content.SetHtmlContent(html.ToString());

            string Id(string? id) => string.IsNullOrEmpty(id) ? "" : $" id=\"{e.Encode(id)}\"";
        }
    }
}
