using ComplianceTracker.Web.Models;
using ComplianceTracker.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ComplianceTracker.UnitTests.Web;

// US-168: edge cases of the shared status colours and the two components. Arrange / Act / Assert.
public class StatusBadgesTests
{
    // #52: a table cell gets the colour of the status, never the badge
    [Theory]
    [InlineData("High", "ct-bg-attention ct-status-high")]
    [InlineData("Critical", "text-bg-danger ct-status-critical")]
    [InlineData(null, "text-bg-light border ct-status-none")]
    public void A_cell_gets_the_colour_without_the_badge(string? status, string expected)
    {
        var css = StatusBadges.CellClassOf(status);

        Assert.Equal(expected, css);
        Assert.DoesNotContain("badge", css.Split(' '));
    }

    [Theory]
    [InlineData("Covered", "text-bg-success")]
    [InlineData("Covered, needs review", "text-bg-warning")]
    [InlineData("Not covered", "text-bg-danger")]
    [InlineData("Not applicable", "text-bg-secondary")]
    [InlineData("Needs action", "ct-bg-attention")]
    [InlineData("AI-generated", "text-bg-info")]
    [InlineData("Critical", "text-bg-danger")]
    [InlineData("High", "ct-bg-attention")]
    [InlineData("Low", "text-bg-success")]
    public void Each_status_has_its_fixed_colour(string status, string colour)
        => Assert.Contains(colour, StatusBadges.ClassOf(status).Split(' '));

    [Theory]
    [InlineData("not covered")]
    [InlineData("  Not covered ")]
    public void Case_and_spaces_do_not_change_the_colour(string status)
        => Assert.Contains("text-bg-danger", StatusBadges.ClassOf(status).Split(' '));

    [Theory]
    [InlineData(null, "ct-status-none")]
    [InlineData("", "ct-status-none")]
    [InlineData("Covered, needs review", "ct-status-covered-needs-review")]
    [InlineData("Development", "ct-status-development")]
    public void The_slug_is_safe_for_a_class_name(string? status, string slug)
        => Assert.Contains(slug, StatusBadges.ClassOf(status).Split(' '));

    [Fact]
    public void An_unknown_status_is_a_plain_label()
        => Assert.Contains("text-bg-light", StatusBadges.ClassOf("Development").Split(' '));

    private static TagHelperOutput Output(string tag, string child = "", params TagHelperAttribute[] attributes)
        => new(tag, new TagHelperAttributeList(attributes), (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(child)));

    private static TagHelperContext Context() => new(new TagHelperAttributeList(), new Dictionary<object, object>(), "x");

    [Fact]
    public void The_badge_keeps_the_page_s_own_class_and_id()
    {
        var output = Output("status-badge", "", new TagHelperAttribute("class", "requirement-status"), new TagHelperAttribute("id", "s1"));

        new StatusBadgeTagHelper { Status = "Covered" }.Process(Context(), output);

        Assert.Equal("span", output.TagName);
        Assert.Contains("requirement-status", output.Attributes["class"].Value.ToString());
        Assert.Contains("text-bg-success", output.Attributes["class"].Value.ToString());
        Assert.Equal("s1", output.Attributes["id"].Value);
        Assert.Equal("Covered", output.Content.GetContent());
    }

    [Fact]
    public async Task The_header_encodes_the_title_and_keeps_the_ids()
    {
        var output = Output("page-header", "<a class=\"btn btn-primary\">Add</a>");

        await new PageHeaderTagHelper { Heading = "<script>x</script>", HeadingId = "risk-title", Status = "High", StatusId = "risk-level" }
            .ProcessAsync(Context(), output);

        var html = output.Content.GetContent();
        Assert.Equal("header", output.TagName);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("id=\"risk-title\"", html);
        Assert.Contains("id=\"risk-level\"", html);
        Assert.Contains("page-header-actions", html);
    }

    [Fact]
    public async Task A_header_without_actions_has_no_empty_action_area()
    {
        var output = Output("page-header", "   ");

        await new PageHeaderTagHelper { Heading = "Documents" }.ProcessAsync(Context(), output);

        Assert.DoesNotContain("page-header-actions", output.Content.GetContent());
    }
}
