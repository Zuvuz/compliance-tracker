using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-068: reads the coverage list (a Markdown file) and compares it with the built-in requirements in the database,
    // which the migrations seed (US-043). Each "## " heading is one regulation; its table rows are | part | text | status | codes |.
    [Binding]
    public class RegulationCoverageStepDefinitions
    {
        private const string InTheTracker = "I trackeren";

        // The heading's start → the built-in framework version it is about
        private static readonly Dictionary<string, Guid> Regulations = new()
        {
            ["CRA"] = BuiltInFrameworks.Cra.VersionId,
            ["AI Act"] = BuiltInFrameworks.AiAct.VersionId
        };

        private readonly DatabaseScenario _database;
        private List<Section> _sections = [];
        private readonly Dictionary<string, HashSet<string>> _builtIn = new();

        public RegulationCoverageStepDefinitions(DatabaseScenario database)
        {
            _database = database;
        }

        [Given("the coverage list in {string}")]
        public void GivenTheCoverageList(string path)
            => _sections = Parse(File.ReadAllText(Path.Combine(Repository.Root(), path)));

        [When("the built-in requirements of each regulation are read")]
        public async Task WhenTheBuiltInRequirementsAreRead()
        {
            await using var db = TestDatabase.CreateContext(_database.ConnectionString);
            foreach (var (name, versionId) in Regulations)
            {
                _builtIn[name] = (await db.Requirements.Where(r => r.FrameworkVersionId == versionId).Select(r => r.Code).ToListAsync()).ToHashSet();
            }
        }

        // R5
        [Then("every code the list marks {string} exists")]
        public void ThenEveryCodeExists(string status)
        {
            var missing = _sections
                .SelectMany(s => s.Rows.Where(r => r.Status == status).SelectMany(r => Codes(r.Codes)).Select(code => (s.Regulation, code)))
                .Where(x => !_builtIn[x.Regulation].Contains(x.code))
                .Select(x => $"{x.code} is missing from the built-in {x.Regulation}")
                .ToList();

            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }

        // Both ways: a requirement added to the seed must also be added to the list
        [Then("every built-in requirement is on the list")]
        public void ThenEveryBuiltInRequirementIsOnTheList()
        {
            var notListed = _sections
                .SelectMany(s => _builtIn[s.Regulation]
                    .Except(s.Rows.Where(r => r.Status == InTheTracker).SelectMany(r => Codes(r.Codes)))
                    .Select(code => $"{s.Regulation} {code} is built in but not on the coverage list"))
                .ToList();

            Assert.True(notListed.Count == 0, string.Join("\n", notListed));
        }

        // R4
        [Then("every line marked {string} links to an issue")]
        public void ThenEveryMissingLineLinksToAnIssue(string status)
        {
            var rows = _sections.SelectMany(s => s.Rows.Where(r => r.Status == status).Select(r => (s.Regulation, r))).ToList();
            Assert.NotEmpty(rows);
            var unlinked = rows.Where(x => !Regex.IsMatch(x.r.Codes, @"\[#\d+\]\(https://github\.com/Zuvuz/compliance-tracker/issues/\d+\)"))
                .Select(x => $"{x.Regulation} {x.r.Part} has no issue")
                .ToList();
            Assert.True(unlinked.Count == 0, string.Join("\n", unlinked));
        }

        // R6
        [Then("every regulation on the list says which text and date it was checked against")]
        public void ThenEveryRegulationSaysWhatItWasCheckedAgainst()
        {
            Assert.Equal(Regulations.Keys.Order(), _sections.Select(s => s.Regulation).Order());
            Assert.All(_sections, s =>
            {
                Assert.Matches(@"\*\*Tjekket mod:\*\* .*eur-lex\.europa\.eu", s.Text);
                Assert.Matches(@"\*\*Tjekket:\*\* \d{2}-\d{2}-\d{4}", s.Text);
            });
        }

        // ---------------------------------------------------------- Parsing

        private sealed record Row(string Part, string Status, string Codes);

        private sealed record Section(string Regulation, string Text, List<Row> Rows);

        private static List<Section> Parse(string markdown)
        {
            var sections = new List<Section>();
            foreach (var chunk in Regex.Split(markdown.Replace("\r", ""), @"(?m)^## ").Skip(1))
            {
                var regulation = Regulations.Keys.FirstOrDefault(k => chunk.StartsWith(k + " ", StringComparison.Ordinal));
                if (regulation is null)
                {
                    continue;
                }

                var rows = chunk.Split('\n')
                    .Where(line => line.StartsWith("| ") && !line.StartsWith("| Del ") && !line.StartsWith("|---"))
                    .Select(line => line.Split('|').Select(c => c.Trim()).ToArray())
                    .Where(cells => cells.Length >= 5)
                    .Select(cells => new Row(cells[1], cells[3], cells[4]))
                    .ToList();
                sections.Add(new Section(regulation, chunk, rows));
            }

            return sections;
        }

        // "I.1, I.2(a), II.8" → the codes; a "Se …" reference to another row has no codes of its own
        private static IEnumerable<string> Codes(string cell)
            => cell.StartsWith("Se ", StringComparison.Ordinal)
                ? []
                : cell.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0);
    }
}
