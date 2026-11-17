using System.Text.RegularExpressions;

namespace ComplianceTracker.UnitTests.Web;

// US-185 R4: the keyboard can open, use and close the shared dialog and the menus, and focus is always visible.
// Edge cases of the markup and the stylesheet. Arrange / Act / Assert.
public class AccessibilityTests
{
    private static string Read(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ComplianceTracker.Web")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine([dir!.FullName, "src", "ComplianceTracker.Web", .. parts]));
    }

    [Fact]
    public void A_rule_that_removes_the_outline_puts_a_visible_focus_ring_in_its_place()
    {
        var css = Read("wwwroot", "css", "site.css");

        foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            var body = rule.Groups[2].Value;
            if (Regex.IsMatch(body, @"outline\s*:\s*(none|0)\b"))
            {
                Assert.True(body.Contains("box-shadow", StringComparison.Ordinal),
                    $"The rule '{rule.Groups[1].Value.Trim()}' hides the focus outline without a focus ring instead.");
            }
        }
    }

    [Fact]
    public void Focus_is_visible_on_buttons_links_fields_and_menus()
    {
        var css = Read("wwwroot", "css", "site.css");

        foreach (var selector in new[] { ".btn:focus-visible", "a:focus-visible", ".form-control:focus", ".form-select:focus", "summary:focus-visible" })
        {
            Assert.Contains(selector, css);
        }
    }

    [Fact]
    public void The_shared_dialog_is_a_named_modal_dialog_that_the_keyboard_can_close()
    {
        var layout = Read("Views", "Shared", "_Layout.cshtml");
        var dialog = Regex.Match(layout, "<div class=\"modal fade\" id=\"shared-dialog\"[^>]*>").Value;

        Assert.Contains("role=\"dialog\"", dialog);
        Assert.Contains("aria-modal=\"true\"", dialog);
        Assert.Contains("aria-labelledby=\"shared-dialog-title\"", dialog);
        Assert.Contains("tabindex=\"-1\"", dialog);
        Assert.DoesNotContain("data-bs-keyboard=\"false\"", dialog); // Esc closes it
        Assert.Contains("id=\"shared-dialog-title\"", layout);
    }

    [Fact]
    public void No_page_takes_an_element_out_of_the_keyboard_order_with_a_positive_tabindex()
    {
        var views = Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(PathOf("Views", "Shared", "_Layout.cshtml")))!, "*.cshtml", SearchOption.AllDirectories);

        Assert.All(views, view => Assert.DoesNotMatch("tabindex=\"[1-9]", File.ReadAllText(view)));
    }

    private static string PathOf(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ComplianceTracker.Web")))
        {
            dir = dir.Parent;
        }

        return Path.Combine([dir!.FullName, "src", "ComplianceTracker.Web", .. parts]);
    }
}
