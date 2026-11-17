using ComplianceTracker.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ComplianceTracker.UnitTests.Web;

// US-172: edge cases of the empty state. Arrange / Act / Assert.
public class EmptyStateTests
{
    private static async Task<(TagHelperOutput Output, string Html)> RenderAsync(EmptyStateTagHelper helper, string actions, params TagHelperAttribute[] attributes)
    {
        var output = new TagHelperOutput("empty-state", new TagHelperAttributeList(attributes),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(actions)));
        await helper.ProcessAsync(new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "x"), output);
        return (output, output.Content.GetContent());
    }

    [Fact]
    public async Task A_member_who_may_act_sees_the_first_steps()
    {
        var (_, html) = await RenderAsync(new EmptyStateTagHelper { Title = "No risks yet", Why = "The CRA asks for it.", CanAct = true, CannotText = "Ask someone" },
            "<a class=\"btn btn-primary\">Add a risk</a>");

        Assert.Contains("empty-state-actions", html);
        Assert.Contains("Add a risk", html);
        Assert.DoesNotContain("Ask someone", html);
    }

    [Fact]
    public async Task A_member_who_may_not_sees_who_can_and_no_buttons()
    {
        var (_, html) = await RenderAsync(new EmptyStateTagHelper { Title = "No risks yet", CanAct = false, CannotText = "A member who manages risks adds them." },
            "<a class=\"btn btn-primary\">Add a risk</a>");

        Assert.DoesNotContain("Add a risk", html);
        Assert.Contains("A member who manages risks adds them.", html);
    }

    [Fact]
    public async Task Without_a_why_or_actions_only_the_title_is_shown()
    {
        var (_, html) = await RenderAsync(new EmptyStateTagHelper { Title = "Nothing" }, " ");

        Assert.DoesNotContain("empty-state-why", html);
        Assert.DoesNotContain("empty-state-actions", html);
    }

    [Fact]
    public async Task The_text_is_encoded_and_the_list_keeps_its_id_and_class()
    {
        var (output, html) = await RenderAsync(new EmptyStateTagHelper { Title = "<b>x</b>", Why = "a & b" }, "",
            new TagHelperAttribute("id", "no-risks"), new TagHelperAttribute("class", "mt-2"));

        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.Contains("a &amp; b", html);
        Assert.Equal("no-risks", output.Attributes["id"].Value);
        Assert.Contains("mt-2", output.Attributes["class"].Value.ToString());
        Assert.Contains("empty-state", output.Attributes["class"].Value.ToString());
    }
}
