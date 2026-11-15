using System.Reflection;
using System.Text.RegularExpressions;
using ComplianceTracker.Web.Diagnostics;

namespace ComplianceTracker.UnitTests.Web;

// US-170: every form that starts an AI action (the actions with [AiRateLimit]) or uploads a file says it works and is not
// sent twice (data-busy, handled by wwwroot/js/site.js). Arrange / Act / Assert.
public partial class BusyFormTests
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

    [GeneratedRegex("""<form\b[^>]*>""")]
    private static partial Regex FormTag();

    // The actions that ask the AI: their names, from the controllers
    private static IReadOnlySet<string> AiActions()
        => typeof(ComplianceTracker.Web.Controllers.ProjectsController).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<AiRateLimitAttribute>() is not null)
            .Select(m => m.Name)
            .ToHashSet();

    public static IReadOnlyList<string> FormsWithoutBusy(string view, IReadOnlySet<string> aiActions)
    {
        var problems = new List<string>();
        foreach (Match form in FormTag().Matches(view))
        {
            var tag = form.Value;
            var action = Regex.Match(tag, """asp-action="([A-Za-z]+)" """.Trim()).Groups[1].Value;
            var needs = aiActions.Contains(action) || tag.Contains("multipart/form-data", StringComparison.Ordinal);
            if (needs && !tag.Contains("data-busy=", StringComparison.Ordinal))
            {
                problems.Add($"line {view[..form.Index].Count(c => c == '\n') + 1}: {(action.Length > 0 ? action : "upload")}");
            }
        }

        return problems;
    }

    [Fact]
    public void The_ai_actions_are_found()
        => Assert.Contains("SuggestMeasures", AiActions());

    [Fact]
    public void Every_ai_and_upload_form_shows_that_it_works()
    {
        var ai = AiActions();
        var problems = Directory.GetFiles(Path.Combine(Web(), "Views"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => FormsWithoutBusy(File.ReadAllText(file), ai).Select(p => $"{Path.GetRelativePath(Web(), file)} {p}"))
            .ToList();

        Assert.True(problems.Count == 0, "Add data-busy to these forms:\n" + string.Join("\n", problems));
    }

    [Theory]
    [InlineData("""<form asp-action="SuggestMeasures" method="post">""", 1)]
    [InlineData("""<form asp-action="SuggestMeasures" method="post" data-busy="Working…">""", 0)]
    [InlineData("""<form asp-action="Upload" method="post" enctype="multipart/form-data">""", 1)]
    [InlineData("""<form asp-action="Rename" method="post">""", 0)]
    public void The_check_finds_ai_and_upload_forms_without_it(string view, int expected)
        => Assert.Equal(expected, FormsWithoutBusy(view, new HashSet<string> { "SuggestMeasures" }).Count);

    [Fact]
    public void The_script_disables_only_the_forms_own_buttons_and_restores_them_on_return()
    {
        var script = File.ReadAllText(Path.Combine(Web(), "wwwroot", "js", "site.js"));

        Assert.Contains("form.dataset.busy", script);
        Assert.Contains("form.querySelectorAll(\"button[type=submit]\")", script);
        Assert.Contains("pageshow", script);
    }
}
