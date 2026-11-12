using System.Text;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Infrastructure.Scanning;

namespace ComplianceTracker.UnitTests.Scanning;

// Unit tests of the SBOM importers and the matching with findings (US-079). Every test follows Arrange / Act / Assert.
public class SbomImporterTests
{
    private static readonly CycloneDxSbomImporter CycloneDx = new();
    private static readonly SpdxSbomImporter Spdx = new();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

    // ---------------------------------------------------------- Picking the format (R1)

    [Fact]
    public void Each_importer_knows_its_own_format_only()
    {
        var cdx = Json("""{"bomFormat":"CycloneDX","specVersion":"1.5","components":[]}""");
        var spdx = Json("""{"spdxVersion":"SPDX-2.3","packages":[]}""");

        Assert.Equal((true, false), (CycloneDx.CanRead("a.json", cdx), Spdx.CanRead("a.json", cdx)));
        Assert.Equal((false, true), (CycloneDx.CanRead("a.json", spdx), Spdx.CanRead("a.json", spdx)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"version":"2.1.0","runs":[]}""")]
    public void Other_files_are_read_by_neither(string content)
        => Assert.False(CycloneDx.CanRead("a.json", Json(content)) || Spdx.CanRead("a.json", Json(content)));

    // ---------------------------------------------------------- CycloneDX

    [Theory]
    [InlineData("1.3")]
    [InlineData("1.2")]
    [InlineData("")]
    public void CycloneDx_before_1_4_is_refused(string version)
    {
        var error = Assert.Throws<DomainException>(() => CycloneDx.Read(Json($$"""{"bomFormat":"CycloneDX","specVersion":"{{version}}"}""")));

        Assert.Equal("Only CycloneDX 1.4 or newer can be read", error.Message);
    }

    [Fact]
    public void CycloneDx_reads_name_version_purl_licence_and_supplier()
    {
        var read = CycloneDx.Read(Json("""
            {"bomFormat":"CycloneDX","specVersion":"1.6","components":[
              {"name":"openssl","version":"3.0.1","purl":"pkg:deb/debian/openssl@3.0.1",
               "licenses":[{"license":{"id":"Apache-2.0"}}],"supplier":{"name":"Debian"}}]}
            """));

        Assert.Equal("CycloneDX 1.6", read.Format);
        Assert.Equal(new SbomComponentRead("openssl", "3.0.1", "pkg:deb/debian/openssl@3.0.1", "Apache-2.0", "Debian"), read.Components.Single());
    }

    [Fact]
    public void CycloneDx_reads_nested_components_and_licence_expressions_and_names()
    {
        var read = CycloneDx.Read(Json("""
            {"bomFormat":"CycloneDX","specVersion":"1.4","components":[
              {"name":"app","licenses":[{"expression":"MIT OR Apache-2.0"}],
               "components":[{"name":"inner","licenses":[{"license":{"name":"Custom licence"}}]}]}]}
            """));

        Assert.Equal(["app", "inner"], read.Components.Select(c => c.Name));
        Assert.Equal(["MIT OR Apache-2.0", "Custom licence"], read.Components.Select(c => c.License));
    }

    [Fact]
    public void CycloneDx_without_components_reads_none_and_skips_nameless_ones()
    {
        Assert.Empty(CycloneDx.Read(Json("""{"bomFormat":"CycloneDX","specVersion":"1.6"}""")).Components);
        Assert.Empty(CycloneDx.Read(Json("""{"bomFormat":"CycloneDX","specVersion":"1.6","components":[{"version":"1.0"},{"name":"  "}]}""")).Components);
    }

    // ---------------------------------------------------------- SPDX

    [Theory]
    [InlineData("SPDX-2.2")]
    [InlineData("SPDX-3.0")]
    public void Spdx_other_than_2_3_is_refused(string version)
        => Assert.Equal("Only SPDX 2.3 can be read",
            Assert.Throws<DomainException>(() => Spdx.Read(Json($$"""{"spdxVersion":"{{version}}","packages":[]}"""))).Message);

    [Fact]
    public void Spdx_reads_the_purl_the_licence_and_the_supplier_name()
    {
        var read = Spdx.Read(Json("""
            {"spdxVersion":"SPDX-2.3","packages":[
              {"name":"lodash","versionInfo":"4.17.21","licenseConcluded":"NOASSERTION","licenseDeclared":"MIT",
               "supplier":"Organization: OpenJS Foundation",
               "externalRefs":[{"referenceType":"cpe23Type","referenceLocator":"cpe:2.3:a:lodash"},
                               {"referenceType":"purl","referenceLocator":"pkg:npm/lodash@4.17.21"}]}]}
            """));

        Assert.Equal(new SbomComponentRead("lodash", "4.17.21", "pkg:npm/lodash@4.17.21", "MIT", "OpenJS Foundation"), read.Components.Single());
    }

    [Fact]
    public void Spdx_unknown_values_are_left_empty()
    {
        var read = Spdx.Read(Json("""{"spdxVersion":"SPDX-2.3","packages":[{"name":"x","licenseConcluded":"NONE","supplier":"NOASSERTION"}]}"""));

        Assert.Equal(new SbomComponentRead("x", null, null, null, null), read.Components.Single());
    }

    // ---------------------------------------------------------- Matching with findings (R6)

    private static Sbom SbomWith(params SbomComponentRead[] components)
        => Sbom.Create(Guid.NewGuid(), "2.1.0", "CycloneDX 1.6", "sbom.json", [1], components, Guid.NewGuid(), Monday);

    private static Finding OpenFinding(string message, FindingSeverity severity = FindingSeverity.Critical, string? location = null)
        => Finding.Create(Guid.NewGuid(), "Trivy", Guid.NewGuid().ToString(), "CVE-2026-1", severity, message, location, Guid.NewGuid());

    [Fact]
    public void A_finding_naming_the_package_and_version_marks_the_component()
    {
        var sbom = SbomWith(new SbomComponentRead("openssl", "3.0.1", null, null, null), new SbomComponentRead("zlib", "1.3.1", null, null, null));

        var views = SbomService.Match(sbom, [OpenFinding("Package: openssl\nInstalled Version: 3.0.1")]);

        Assert.Equal("1 open finding (critical)", views.Single(v => v.Component.Name == "openssl").FindingsText);
        Assert.Null(views.Single(v => v.Component.Name == "zlib").FindingsText);
    }

    [Fact]
    public void Another_version_of_the_package_is_not_marked()
    {
        var sbom = SbomWith(new SbomComponentRead("openssl", "3.0.2", null, null, null));

        Assert.Empty(SbomService.Match(sbom, [OpenFinding("Package: openssl\nInstalled Version: 3.0.1")]).Single().OpenFindings);
    }

    [Fact]
    public void A_longer_package_name_is_not_a_match()
    {
        var sbom = SbomWith(new SbomComponentRead("openssl", "3.0.1", null, null, null));

        Assert.Empty(SbomService.Match(sbom, [OpenFinding("Package: openssl-libs\nInstalled Version: 3.0.1")]).Single().OpenFindings);
    }

    [Fact]
    public void The_name_may_be_in_the_location_and_case_does_not_matter()
    {
        var sbom = SbomWith(new SbomComponentRead("Npgsql", null, null, null, null));

        var view = SbomService.Match(sbom, [OpenFinding("A vulnerable package", FindingSeverity.Medium, "NPGSQL/package.lock.json")]).Single();

        Assert.Equal("1 open finding", view.FindingsText);
    }

    [Fact]
    public void Closed_findings_do_not_mark_and_several_are_counted_with_the_worst()
    {
        var sbom = SbomWith(new SbomComponentRead("lodash", "4.17.21", null, null, null));
        var fixedOne = OpenFinding("lodash 4.17.21");
        fixedOne.MissingFrom(Guid.NewGuid(), Monday);

        var view = SbomService.Match(sbom, [fixedOne, OpenFinding("lodash 4.17.21", FindingSeverity.High), OpenFinding("lodash 4.17.21", FindingSeverity.Low)]).Single();

        Assert.Equal("2 open findings (high)", view.FindingsText);
    }

    // ---------------------------------------------------------- The SBOM (R2, R3)

    [Fact]
    public void The_same_component_is_listed_once_and_nameless_ones_are_skipped()
    {
        var sbom = SbomWith(
            new SbomComponentRead("lodash", "4.17.21", "pkg:npm/lodash@4.17.21", null, null),
            new SbomComponentRead("lodash", "4.17.21", "pkg:npm/lodash@4.17.21", null, null),
            new SbomComponentRead(" ", null, null, null, null));

        Assert.Equal("CycloneDX 1.6 · 1 component · version 2.1.0", sbom.Summary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void The_product_version_is_needed(string? version)
        => Assert.Equal("Enter the product version the SBOM is for",
            Assert.Throws<DomainException>(() => Sbom.Create(Guid.NewGuid(), version, "SPDX 2.3", "a.json", [1], [], Guid.NewGuid(), Monday)).Message);

    [Fact]
    public void A_version_over_100_characters_and_an_empty_file_are_refused()
    {
        Assert.Throws<DomainException>(() => Sbom.Create(Guid.NewGuid(), new string('1', 101), "SPDX 2.3", "a.json", [1], [], Guid.NewGuid(), Monday));
        Assert.Equal("The file is empty",
            Assert.Throws<DomainException>(() => Sbom.Create(Guid.NewGuid(), "1.0", "SPDX 2.3", "a.json", [], [], Guid.NewGuid(), Monday)).Message);
    }

    [Fact]
    public void The_newest_SBOM_of_each_version_is_shown()
    {
        var project = Guid.NewGuid();
        var old = Sbom.Create(project, "2.1.0", "SPDX 2.3", "a.json", [1], [], Guid.NewGuid(), Monday);
        var replacement = Sbom.Create(project, "2.1.0", "SPDX 2.3", "b.json", [2], [], Guid.NewGuid(), Monday.AddDays(1));
        var other = Sbom.Create(project, "2.0.0", "SPDX 2.3", "c.json", [3], [], Guid.NewGuid(), Monday.AddHours(1));

        var latest = SbomService.Latest([old, replacement, other]);

        Assert.Equal([replacement.Id, other.Id], latest.Select(s => s.Id));
    }
}
