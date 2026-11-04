using ComplianceTracker.Specs.Support;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-094: reads the app's own source code and fails on code that opens the door to injection.
    // Comments are left out, so a comment may name a forbidden call. Migrations, bin and obj are not read.
    // An exception goes in Reviewed below, with its reason, and in docs/sikker-kode.md.
    [Binding]
    public class InjectionGuardrailsStepDefinitions
    {
        // (file, rule) → why it is safe. The file is relative to the repository, with forward slashes.
        private static readonly Dictionary<(string File, string Rule), string> Reviewed = new()
        {
            [("src/ComplianceTracker.Web/wwwroot/js/work-items.js", "innerHTML")] =
                "Inserts the work item list from the tracker's own page (/Projects/WorkItems), which Razor has already encoded (US-018 R7)"
        };

        private List<SourceFile> _files = [];

        [When("the app's source code is read")]
        public void WhenTheSourceCodeIsRead()
        {
            var root = Repository.Root();
            var src = Path.Combine(root, "src");
            _files = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml") || f.EndsWith(".js"))
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/") && !f.Contains("/Migrations/") && !f.Contains("/wwwroot/lib/"))
                .Select(f => new SourceFile(f, WithoutComments(f, File.ReadAllText(Path.Combine(root, f)))))
                .ToList();

            Assert.NotEmpty(_files);
        }

        // R1: EF Core's LINQ, FromSql and ExecuteSql always send values as parameters; the "Raw" methods take SQL as text
        [Then("no code builds SQL as text")]
        public void ThenNoCodeBuildsSqlAsText()
            => AssertNone("raw SQL", ".cs", @"\b(FromSqlRaw|ExecuteSqlRaw(Async)?|SqlQueryRaw|CommandText)\b");

        // R2: Razor encodes everything it writes, except Html.Raw and HtmlString
        [Then("no view writes unencoded HTML")]
        public void ThenNoViewWritesUnencodedHtml()
            => AssertNone("Html.Raw", ".cshtml", @"\bHtml\.Raw\s*\(|\bHtmlString\s*\(");

        // R3
        [Then("no code starts a process")]
        public void ThenNoCodeStartsAProcess()
            => AssertNone("process", ".cs", @"\bProcess\.Start\b|\bProcessStartInfo\b");

        // R4: RedirectToAction and LocalRedirect stay inside the app; Redirect takes any address
        [Then("no code redirects to an address from input")]
        public void ThenNoCodeRedirectsToAnAddressFromInput()
            => AssertNone("redirect", ".cs", @"\bRedirect(Permanent)?(PreserveMethod)?\s*\(|\bnew\s+RedirectResult\s*\(");

        // R5: uploads are stored in the database by id, so no file name from a user ever becomes a path
        [Then("no code reads or writes files on the server")]
        public void ThenNoCodeReadsOrWritesFiles()
            => AssertNone("file system", ".cs",
                @"(?<![\w.])(File|Directory)\.(Open|Create|Write|Read|Append|Delete|Move|Copy|Exists|Enumerate|Get)\w*\s*\(|\bnew\s+FileStream\s*\(|\bPath\.Combine\s*\(");

        // R6
        [Then("no script uses eval, new Function or HTML from input")]
        public void ThenNoScriptRunsTextAsCode()
        {
            AssertNone("eval", ".js", @"\beval\s*\(|\bnew\s+Function\s*\(|\bdocument\.write\s*\(");
            AssertNone("innerHTML", ".js", @"\.(innerHTML|outerHTML)\s*=|\binsertAdjacentHTML\s*\(");
        }

        // Every match outside the reviewed list, as "file:line: code"
        private void AssertNone(string rule, string extension, string pattern)
        {
            var regex = new Regex(pattern);
            var found = _files
                .Where(f => f.Path.EndsWith(extension) && !Reviewed.ContainsKey((f.Path, rule)))
                .SelectMany(f => f.Code.Split('\n').Select((line, i) => (f.Path, Line: i + 1, Code: line.Trim())))
                .Where(l => regex.IsMatch(l.Code))
                .Select(l => $"{l.Path}:{l.Line}: {l.Code}")
                .ToList();

            Assert.True(found.Count == 0,
                $"Not allowed ({rule}). Use the safe way, or add the place to the reviewed list with its reason (docs/sikker-kode.md):\n"
                + string.Join("\n", found));
        }

        // Comments are blanked but their line breaks are kept, so line numbers stay right
        private static string WithoutComments(string path, string text)
        {
            static string Blank(Match m) => new(m.Value.Where(c => c == '\n').ToArray());

            if (path.EndsWith(".cshtml"))
            {
                text = Regex.Replace(text, @"@\*.*?\*@", Blank, RegexOptions.Singleline);
            }

            text = Regex.Replace(text, @"/\*.*?\*/", Blank, RegexOptions.Singleline);
            return string.Join('\n', text.Split('\n').Select(line => line.TrimStart().StartsWith("//") ? "" : line));
        }

        private sealed record SourceFile(string Path, string Code);
    }
}
