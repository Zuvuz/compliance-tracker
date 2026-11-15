using System.Text.RegularExpressions;

namespace ComplianceTracker.UnitTests.Web;

// US-167: no page is wider than the window. These tests read the views and the stylesheet and catch the usual causes
// before they reach a browser: a row of controls with a select that cannot wrap, a top bar that unfolds too early, and the
// stylesheet's guards going missing. Arrange / Act / Assert.
public partial class ViewLayoutTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ComplianceTracker.Web")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    private static string Web => Path.Combine(Root(), "src", "ComplianceTracker.Web");

    [GeneratedRegex("""<(form|div)\b[^>]*class="([^"]*\bd-flex\b[^"]*)"[^>]*>""")]
    private static partial Regex FlexRow();

    // A flex row (form or div) that holds a select, and none of its own kind inside, must be able to wrap
    public static IReadOnlyList<string> RowsThatCannotWrap(string view)
    {
        var problems = new List<string>();
        foreach (Match m in FlexRow().Matches(view))
        {
            var css = m.Groups[2].Value;
            if (css.Contains("flex-wrap", StringComparison.Ordinal) || css.Contains("flex-column", StringComparison.Ordinal))
            {
                continue;
            }

            var tag = m.Groups[1].Value;
            var end = view.IndexOf($"</{tag}>", m.Index + m.Length, StringComparison.Ordinal);
            var body = view[(m.Index + m.Length)..(end > 0 ? end : Math.Min(view.Length, m.Index + m.Length + 800))];
            if (body.Contains("<select", StringComparison.Ordinal) && !body.Contains($"<{tag}", StringComparison.Ordinal))
            {
                problems.Add($"line {view[..m.Index].Count(c => c == '\n') + 1}: class=\"{css}\"");
            }
        }

        return problems;
    }

    [Fact]
    public void Every_row_with_a_select_in_the_views_can_wrap()
    {
        var problems = Directory.GetFiles(Path.Combine(Web, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => RowsThatCannotWrap(File.ReadAllText(file)).Select(p => $"{Path.GetRelativePath(Web, file)} {p}"))
            .ToList();

        Assert.True(problems.Count == 0, "Add flex-wrap to these rows:\n" + string.Join("\n", problems));
    }

    [Theory]
    [InlineData("""<form class="d-flex gap-1"><select></select></form>""", 1)]
    [InlineData("""<form class="d-flex flex-wrap gap-1"><select></select></form>""", 0)]
    [InlineData("""<div class="d-flex flex-column"><select></select></div>""", 0)]
    [InlineData("""<div class="d-flex gap-1"><input /><button></button></div>""", 0)]
    public void The_check_finds_only_rows_with_a_select_that_cannot_wrap(string view, int expected)
        => Assert.Equal(expected, RowsThatCannotWrap(view).Count);

    [Fact]
    public void The_top_bar_folds_into_its_button_below_the_large_breakpoint_and_its_items_wrap()
    {
        var layout = File.ReadAllText(Path.Combine(Web, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("navbar-expand-lg", layout);
        Assert.DoesNotContain("navbar-expand-sm", layout);
        Assert.Contains("navbar-nav flex-wrap", layout);
    }

    [Fact]
    public void The_stylesheet_keeps_selects_and_tables_inside_the_page()
    {
        var css = File.ReadAllText(Path.Combine(Web, "wwwroot", "css", "site.css"));

        Assert.Contains("select, .form-select, .form-control { max-width: 100%; }", css);
        Assert.Contains("main table.table { display: block; overflow-x: auto; }", css);
    }
}
