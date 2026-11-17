namespace ComplianceTracker.UnitTests.Web;

// US-175: the script that remembers the choices keeps its rules: in the browser only, an address with its own filters
// wins, a remembered tab must be one the project has, and a restore happens once (no loop). Arrange / Act / Assert.
public class RememberChoicesTests
{
    private static string Script()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "ComplianceTracker.Web")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "ComplianceTracker.Web", "wwwroot", "js", "site.js"));
    }

    private static string Part()
    {
        var script = Script();
        var start = script.IndexOf("// US-175:", StringComparison.Ordinal);
        Assert.True(start >= 0, "site.js has no US-175 part");
        return script[start..];
    }

    [Fact]
    public void Nothing_is_sent_to_the_server()
    {
        var part = Part();
        Assert.Contains("localStorage", part);
        Assert.DoesNotContain("fetch(", part);
        Assert.DoesNotContain("XMLHttpRequest", part);
    }

    [Fact]
    public void A_remembered_tab_must_be_valid_and_a_visit_from_the_project_itself_is_left_alone()
    {
        var part = Part();
        Assert.Contains("valid.includes(remembered)", part);
        Assert.Contains("cameFromProject", part);
    }

    [Fact]
    public void An_address_with_its_own_filters_wins_and_a_restore_happens_once()
    {
        var part = Part();
        Assert.Contains("own.length > 0", part);
        Assert.Contains("once(", part);
    }

    [Fact]
    public void Storage_that_cannot_be_used_does_not_break_the_page()
        => Assert.Contains("catch { /* private window */ }", Part());
}
