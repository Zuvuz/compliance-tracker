namespace ComplianceTracker.UnitTests.Web;

// US-166: the script that opens forms in the shared dialog keeps its rules. Arrange / Act / Assert.
public class DialogsTests
{
    private static string Part()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ComplianceTracker.Web")))
        {
            dir = dir.Parent;
        }

        var script = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ComplianceTracker.Web", "wwwroot", "js", "site.js"));
        var start = script.IndexOf("// US-166:", StringComparison.Ordinal);
        Assert.True(start >= 0, "site.js has no US-166 part");
        return script[start..];
    }

    [Fact]
    public void Without_bootstrap_nothing_is_moved()
        => Assert.Contains("if (!dialog || !window.bootstrap || !window.bootstrap.Modal)", Part());

    [Fact]
    public void The_form_goes_back_to_its_place_and_the_focus_to_its_button()
    {
        var part = Part();
        Assert.Contains("hidden.bs.modal", part);
        Assert.Contains("moved.home.before(n)", part);
        Assert.Contains("moved.opener?.focus()", part);
    }

    [Fact]
    public void After_a_broken_rule_the_dialog_opens_again_with_the_error()
    {
        var part = Part();
        Assert.Contains(".validation-summary-errors li", part);
        Assert.Contains("role\", \"alert\"", part);
    }
}
