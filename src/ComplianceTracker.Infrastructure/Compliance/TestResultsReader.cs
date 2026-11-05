using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ComplianceTracker.Infrastructure.Compliance
{
    // US-058 R1, R9: reads TRX (dotnet test --logger trx) and JUnit XML. A test's tags come from what the format has:
    // TRX's <TestCategory> items (MSTest, NUnit), JUnit's <property name="Category|tag|tags"> and, for runners that put
    // no tags in the file (xUnit), a line "Requirement tags: @cra-I.2(d) @iso27001-A.5.15" in the test's output (B45).
    // XML is read without DTDs or external entities (ASVS V5.5.2).
    public partial class TestResultsReader : ITestResultsReader
    {
        public const string TagsLinePrefix = "Requirement tags:";

        public IReadOnlyList<TestCaseResult> Read(byte[] content)
        {
            XDocument document;
            try
            {
                using var stream = new MemoryStream(content);
                using var xml = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                document = XDocument.Load(xml);
            }
            catch (XmlException)
            {
                throw new DomainException("The file is not test results (TRX or JUnit XML)");
            }

            return document.Root?.Name.LocalName switch
            {
                "TestRun" => Trx(document),
                "testsuites" or "testsuite" => JUnit(document),
                _ => throw new DomainException("The file is not test results (TRX or JUnit XML)")
            };
        }

        private static List<TestCaseResult> Trx(XDocument document)
        {
            var ns = document.Root!.Name.Namespace;
            var categories = document.Descendants(ns + "UnitTest").ToDictionary(
                t => (string?)t.Attribute("id") ?? "",
                t => t.Descendants(ns + "TestCategoryItem").Select(c => (string?)c.Attribute("TestCategory") ?? "").ToList());

            return document.Descendants(ns + "UnitTestResult")
                .Select(r =>
                {
                    var tags = categories.GetValueOrDefault((string?)r.Attribute("testId") ?? "") ?? [];
                    var output = r.Element(ns + "Output")?.Element(ns + "StdOut")?.Value;
                    return new TestCaseResult(
                        (string?)r.Attribute("testName") ?? "",
                        (string?)r.Attribute("outcome") == "Passed",
                        tags.Concat(TagsFromOutput(output)).ToList());
                })
                .Where(t => t.Name.Length > 0)
                .ToList();
        }

        private static List<TestCaseResult> JUnit(XDocument document)
            => document.Descendants("testcase")
                .Select(c =>
                {
                    var properties = c.Descendants("property")
                        .Where(p => ((string?)p.Attribute("name"))?.ToLowerInvariant() is "category" or "tag" or "tags")
                        .SelectMany(p => ((string?)p.Attribute("value") ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries));
                    var failed = c.Element("failure") is not null || c.Element("error") is not null || c.Element("skipped") is not null;
                    return new TestCaseResult(
                        (string?)c.Attribute("name") ?? "",
                        !failed,
                        properties.Concat(TagsFromOutput(c.Element("system-out")?.Value)).ToList());
                })
                .ToList();

        private static IEnumerable<string> TagsFromOutput(string? output)
            => output is null
                ? []
                : TagsLine().Matches(output).SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        [GeneratedRegex(@"Requirement tags:([^\r\n]*)")]
        private static partial Regex TagsLine();
    }
}
