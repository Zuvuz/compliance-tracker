using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Infrastructure.Scanning;
using System.Text;

namespace ComplianceTracker.UnitTests.Scanning;

// Edge cases of the SARIF importer (US-078): what is a SARIF file, severity limits, fingerprints and missing parts.
// Every test follows Arrange / Act / Assert.
public class SarifScanImporterTests
{
    private readonly SarifScanImporter _importer = new();

    private static byte[] Json(string json) => Encoding.UTF8.GetBytes(json);

    // One run with one rule and one result; parts can be left out by passing null
    private static byte[] Sarif(string? tool = "Trivy", string? ruleSeverity = "\"7.5\"", string? resultLevel = null,
        string? fingerprints = "\"partialFingerprints\": { \"hash\": \"abc\" },", string? location = "{ \"physicalLocation\": { \"artifactLocation\": { \"uri\": \"a.json\" }, \"region\": { \"startLine\": 3 } } }",
        string message = "A vulnerable package")
    {
        var driver = tool is null ? "{}" : $"{{ \"name\": \"{tool}\", \"rules\": [ {{ \"id\": \"R1\"{(ruleSeverity is null ? "" : $", \"properties\": {{ \"security-severity\": {ruleSeverity} }}")} }} ] }}";
        var level = resultLevel is null ? "" : $"\"level\": \"{resultLevel}\",";
        var locations = location is null ? "" : $", \"locations\": [ {location} ]";
        return Json($$"""
            { "version": "2.1.0", "runs": [ { "tool": { "driver": {{driver}} },
              "results": [ { "ruleId": "R1", {{level}} {{fingerprints}} "message": { "text": "{{message}}" }{{locations}} } ] } ] }
            """);
    }

    // ---------------------------------------------------------- What is a SARIF file

    [Theory]
    [InlineData("scan.sarif")]
    [InlineData("scan.SARIF")]
    [InlineData("results.json")]
    public void A_SARIF_2_1_0_file_is_read_by_sarif_and_json_names(string fileName)
    {
        // Arrange
        var content = Sarif();

        // Act
        var readable = _importer.CanRead(fileName, content);

        // Assert
        Assert.True(readable);
    }

    [Theory]
    [InlineData("report.html")]
    [InlineData("scan.txt")]
    [InlineData("scan")]
    [InlineData("")]
    public void Other_file_names_are_refused_even_with_SARIF_content(string fileName)
    {
        // Arrange
        var content = Sarif();

        // Act
        var readable = _importer.CanRead(fileName, content);

        // Assert
        Assert.False(readable);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{ \"version\": \"2.0.0\", \"runs\": [] }")]
    [InlineData("{ \"version\": \"2.1.0\" }")]
    [InlineData("{ \"version\": \"2.1.0\", \"runs\": {} }")]
    [InlineData("{ \"version\": 2.1, \"runs\": [] }")]
    public void Content_that_is_not_SARIF_2_1_0_is_refused(string json)
    {
        // Arrange
        var content = Json(json);

        // Act
        var readable = _importer.CanRead("scan.sarif", content);

        // Assert
        Assert.False(readable);
    }

    [Fact]
    public void Reading_broken_JSON_gives_a_message_for_the_user()
    {
        // Arrange
        var content = Json("{ broken");

        // Act
        var error = Assert.Throws<DomainException>(() => _importer.Read(content));

        // Assert
        Assert.Equal("The SARIF file could not be read", error.Message);
    }

    [Theory]
    [InlineData("""{ "version": "2.1.0", "runs": [ 42 ] }""")]
    [InlineData("""{ "version": "2.1.0", "runs": [ { "results": [ "text" ] } ] }""")]
    public void Odd_types_inside_a_SARIF_file_give_a_message_not_a_crash(string json)
    {
        // Arrange
        var content = Json(json);

        // Act
        var error = Assert.Throws<DomainException>(() => _importer.Read(content));

        // Assert
        Assert.Equal("The SARIF file could not be read", error.Message);
    }

    [Fact]
    public void Numbers_where_text_is_expected_are_left_out()
    {
        // Arrange
        var content = Json("""
            { "version": "2.1.0", "runs": [ { "tool": { "driver": { "name": 7 } },
              "results": [ { "ruleId": 5, "message": { "text": 9 }, "locations": [ { "physicalLocation": { "artifactLocation": { "uri": 1 }, "region": { "startLine": "x" } } } ] } ] } ] }
            """);

        // Act
        var report = _importer.Read(content);

        // Assert
        var result = report.Results.Single();
        Assert.Equal("Unknown tool", report.Tool);
        Assert.Equal("", result.RuleId);
        Assert.Equal("", result.Message);
        Assert.Null(result.Location);
    }

    // ---------------------------------------------------------- Severity (R2)

    [Theory]
    [InlineData("\"10.0\"", FindingSeverity.Critical)]
    [InlineData("\"9.0\"", FindingSeverity.Critical)]
    [InlineData("\"8.9\"", FindingSeverity.High)]
    [InlineData("\"7.0\"", FindingSeverity.High)]
    [InlineData("\"6.9\"", FindingSeverity.Medium)]
    [InlineData("\"4.0\"", FindingSeverity.Medium)]
    [InlineData("\"3.9\"", FindingSeverity.Low)]
    [InlineData("\"0\"", FindingSeverity.Low)]
    [InlineData("9.8", FindingSeverity.Critical)]
    public void The_security_severity_decides_at_its_limits(string score, FindingSeverity severity)
    {
        // Arrange
        var content = Sarif(ruleSeverity: score);

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Equal(severity, result.Severity);
    }

    [Theory]
    [InlineData("error", FindingSeverity.High)]
    [InlineData("warning", FindingSeverity.Medium)]
    [InlineData("note", FindingSeverity.Low)]
    [InlineData("none", FindingSeverity.Low)]
    public void Without_a_security_severity_the_level_decides(string level, FindingSeverity severity)
    {
        // Arrange
        var content = Sarif(ruleSeverity: null, resultLevel: level);

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Equal(severity, result.Severity);
    }

    [Fact]
    public void A_security_severity_that_is_not_a_number_falls_back_to_the_level()
    {
        // Arrange
        var content = Sarif(ruleSeverity: "\"high\"", resultLevel: "warning");

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Equal(FindingSeverity.Medium, result.Severity);
    }

    [Fact]
    public void Without_a_severity_or_a_level_a_finding_is_low()
    {
        // Arrange
        var content = Sarif(ruleSeverity: null, resultLevel: null);

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Equal(FindingSeverity.Low, result.Severity);
    }

    // ---------------------------------------------------------- Fingerprints (R3)

    [Fact]
    public void The_tools_own_fingerprint_is_used()
    {
        // Arrange
        var content = Sarif();

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Equal("hash:abc", result.Fingerprint);
    }

    [Fact]
    public void Without_a_fingerprint_the_same_result_gets_the_same_one_every_time()
    {
        // Arrange
        var content = Sarif(fingerprints: "");

        // Act
        var first = _importer.Read(content).Results.Single().Fingerprint;
        var second = _importer.Read(content).Results.Single().Fingerprint;

        // Assert
        Assert.StartsWith("sha256:", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Without_a_fingerprint_another_message_gives_another_one()
    {
        // Arrange
        var one = Sarif(fingerprints: "", message: "Package A");
        var other = Sarif(fingerprints: "", message: "Package B");

        // Act
        var first = _importer.Read(one).Results.Single().Fingerprint;
        var second = _importer.Read(other).Results.Single().Fingerprint;

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void An_empty_fingerprint_is_not_used()
    {
        // Arrange
        var content = Sarif(fingerprints: "\"fingerprints\": { \"x\": \"\" },");

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.StartsWith("sha256:", result.Fingerprint);
    }

    // ---------------------------------------------------------- Missing parts

    [Fact]
    public void A_run_without_a_tool_name_is_from_an_unknown_tool()
    {
        // Arrange
        var content = Sarif(tool: null);

        // Act
        var report = _importer.Read(content);

        // Assert
        Assert.Equal("Unknown tool", report.Tool);
    }

    [Fact]
    public void A_result_without_a_location_has_none()
    {
        // Arrange
        var content = Sarif(location: null);

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Null(result.Location);
    }

    [Fact]
    public void A_location_without_a_line_is_only_the_file()
    {
        // Arrange
        var content = Sarif(location: "{ \"physicalLocation\": { \"artifactLocation\": { \"uri\": \"a.json\" } } }");

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Equal("a.json", result.Location);
    }

    [Fact]
    public void A_location_with_a_line_names_both()
    {
        // Arrange
        var content = Sarif();

        // Act
        var result = _importer.Read(content).Results.Single();

        // Assert
        Assert.Equal("a.json:3", result.Location);
    }

    [Fact]
    public void A_run_without_results_gives_none()
    {
        // Arrange
        var content = Json("""{ "version": "2.1.0", "runs": [ { "tool": { "driver": { "name": "Trivy" } } } ] }""");

        // Act
        var report = _importer.Read(content);

        // Assert
        Assert.Empty(report.Results);
        Assert.Equal("Trivy", report.Tool);
    }

    [Fact]
    public void Every_run_is_read_and_the_first_tool_name_is_used()
    {
        // Arrange
        var content = Json("""
            { "version": "2.1.0", "runs": [
              { "tool": { "driver": { "name": "Trivy" } }, "results": [ { "ruleId": "A", "message": { "text": "a" } } ] },
              { "tool": { "driver": { "name": "Grype" } }, "results": [ { "ruleId": "B", "message": { "text": "b" } } ] } ] }
            """);

        // Act
        var report = _importer.Read(content);

        // Assert
        Assert.Equal("Trivy", report.Tool);
        Assert.Equal(["A", "B"], report.Results.Select(r => r.RuleId));
    }
}
