using System.Text.RegularExpressions;

namespace ComplianceTracker.UnitTests.Web;

// US-171: every message on a page is announced to screen readers, and a success fades while warnings and errors stay.
// These tests read the views, the layout and the script. Arrange / Act / Assert.
public partial class MessagesTests
{
    private static string Web()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ComplianceTracker.Web")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found"), "src", "ComplianceTracker.Web");
    }

    [GeneratedRegex("""<(div|p|section)\b(?=[^>]*\bclass="[^"]*\balert alert-(success|info|secondary|warning|danger)\b)[^>]*>""", RegexOptions.Singleline)]
    private static partial Regex Alert();

    // R3: a success or an info is a status and an error an alert. A warning is an alert after an action, or a status when
    // it is a standing notice on the page (e.g. "Needs attention"), so it is not read out at every page load.
    public static IReadOnlyList<string> WrongRoles(string view)
        => Alert().Matches(view)
            .Where(m => m.Groups[2].Value switch
            {
                "danger" => !m.Value.Contains("role=\"alert\"", StringComparison.Ordinal),
                "warning" => !m.Value.Contains("role=\"alert\"", StringComparison.Ordinal) && !m.Value.Contains("role=\"status\"", StringComparison.Ordinal),
                _ => !m.Value.Contains("role=\"status\"", StringComparison.Ordinal)
            })
            .Select(m => $"line {view[..m.Index].Count(c => c == '\n') + 1}: {m.Value}")
            .ToList();

    [Fact]
    public void Every_message_in_the_views_is_announced_with_the_right_role()
    {
        var problems = Directory.GetFiles(Path.Combine(Web(), "Views"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => WrongRoles(File.ReadAllText(file)).Select(p => $"{Path.GetRelativePath(Web(), file)} {p}"))
            .ToList();

        Assert.True(problems.Count == 0, "Give these messages role=\"status\" (success, info) or role=\"alert\" (warning, error):\n" + string.Join("\n", problems));
    }

    [Theory]
    [InlineData("""<div class="alert alert-success">Saved</div>""", 1)]
    [InlineData("""<div class="alert alert-success" role="alert">Saved</div>""", 1)]
    [InlineData("""<div class="alert alert-danger" role="status">Failed</div>""", 1)]
    [InlineData("""<div class="alert alert-warning mt-3" id="x" role="alert">Careful</div>""", 0)]
    [InlineData("<div class=\"alert alert-info\"\n     role=\"status\">Info</div>", 0)]
    public void A_message_with_a_missing_or_wrong_role_is_found(string view, int problems)
        => Assert.Equal(problems, WrongRoles(view).Count);

    [Fact]
    public void The_layout_has_one_place_for_messages_above_the_page()
    {
        var layout = File.ReadAllText(Path.Combine(Web(), "Views", "Shared", "_Layout.cshtml"));

        Assert.True(layout.IndexOf("<partial name=\"_Messages\" />", StringComparison.Ordinal) < layout.IndexOf("@RenderBody()", StringComparison.Ordinal));
    }

    [Fact]
    public void A_success_fades_after_six_seconds_unless_it_is_kept()
    {
        var script = File.ReadAllText(Path.Combine(Web(), "wwwroot", "js", "site.js"));

        Assert.Contains(".alert-success:not([data-keep])", script);
        Assert.Contains("6000", script);
        Assert.Contains("prefers-reduced-motion", script);
    }

    // A message that is a result to read or a link to copy must not fade
    [Theory]
    [InlineData("Users", "Index.cshtml", "id=\"invitation-link\"")]
    [InlineData("LogCheck", "Index.cshtml", "id=\"log-check-result\" data-intact=\"true\"")]
    public void A_result_or_a_link_is_kept(string folder, string file, string marker)
    {
        var view = File.ReadAllText(Path.Combine(Web(), "Views", folder, file));
        var at = view.IndexOf(marker, StringComparison.Ordinal);
        var start = view.LastIndexOf("alert alert-success", at, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.Contains("data-keep", view[start..Math.Max(start, view.IndexOf('>', start) + 1)] + view[start..at]);
    }
}
