using System.Text;
using AngleSharp.Dom;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-079: the SBOM section of the Scans tab, through the web app like a user.
    // UI contract: Views/Projects/_ScansTab.cshtml (#upload-sbom, #sboms rows .sbom[data-version] with .sbom-summary and
    // form .sbom-as-evidence, #sbom-components rows .sbom-component[data-name] with .component-purl, .component-license
    // and .component-findings).
    [Binding]
    public class SbomImportStepDefinitions
    {
        private const string Project = "Acme Portal";

        private readonly WebScenario _web;

        public SbomImportStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I upload the CycloneDX SBOM {string} with {int} components for version {string}")]
        [When("I upload the CycloneDX SBOM {string} with {int} components for version {string}")]
        public Task WhenIUploadCycloneDx(string fileName, int count, string version)
            => UploadAsync(fileName, version, TestSboms.CycloneDx(count));

        [When("I upload the SPDX SBOM {string} with {int} components for version {string}")]
        public Task WhenIUploadSpdx(string fileName, int count, string version)
            => UploadAsync(fileName, version, TestSboms.Spdx(count));

        [When("I upload the file {string} as the SBOM for version {string}")]
        public Task WhenIUploadAnotherFile(string fileName, string version)
            => UploadAsync(fileName, version, Encoding.UTF8.GetBytes("{\"notes\": \"Not an SBOM\"}"));

        [When("I upload a CycloneDX 1.3 SBOM for version {string}")]
        public Task WhenIUploadAnOldCycloneDx(string version)
            => UploadAsync("old.cdx.json", version, TestSboms.CycloneDx(2, "1.3"));

        // A finding as Trivy writes it: the package and its installed version in the message
        [Given("{string} has a {string} scan with a critical finding in {string} {string}")]
        public async Task GivenAScanWithAFindingIn(string project, string tool, string package, string version)
        {
            await OpenTabAsync();
            var form = _web.Page!.QuerySelector("#upload-scan")!;
            var fields = Hidden(form);
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, "trivy.sarif",
                TestScans.Sarif(tool, new TestScans.Result("CVE-2026-3000", 9.8, $"Package: {package}\nInstalled Version: {version}\nA buffer overflow")));
        }

        [When("I use the SBOM for version {string} as evidence for {string}")]
        public async Task WhenIUseTheSbomAsEvidence(string version, string code)
        {
            await OpenTabAsync();
            var form = Sbom(version).QuerySelector("form.sbom-as-evidence")
                ?? throw new Xunit.Sdk.XunitException($"The SBOM for {version} cannot be used as evidence.");
            var option = form.QuerySelectorAll("option").FirstOrDefault(o => o.TextContent.Trim().EndsWith(" " + code, StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException($"'{code}' cannot be chosen.");
            var fields = Hidden(form);
            fields["requirementId"] = option.GetAttribute("value")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        [Then("the SBOM for version {string} says {string}")]
        public async Task ThenTheSbomSays(string version, string summary)
        {
            await OpenTabAsync();
            Assert.Equal(summary, Sbom(version).QuerySelector(".sbom-summary")!.TextContent.Trim());
        }

        [Then("the SBOM for version {string} lists {int} components")]
        public async Task ThenTheSbomLists(string version, int count)
        {
            await OpenTabAsync(version);
            Assert.Equal(count, _web.Page!.QuerySelectorAll("#sbom-components tr.sbom-component").Length);
        }

        [Then("the component {string} has the package URL {string} and the licence {string}")]
        public async Task ThenTheComponentHas(string name, string purl, string license)
        {
            var row = await ComponentAsync(name);
            Assert.Equal((purl, license), (row.QuerySelector(".component-purl")!.TextContent.Trim(), row.QuerySelector(".component-license")!.TextContent.Trim()));
        }

        [Then("the component {string} shows {string}")]
        public async Task ThenTheComponentShows(string name, string text)
            => Assert.Equal(text, (await ComponentAsync(name)).QuerySelector(".component-findings")!.TextContent.Trim());

        [Then("the component {string} shows no findings")]
        public async Task ThenTheComponentShowsNoFindings(string name)
            => Assert.Equal("", (await ComponentAsync(name)).QuerySelector(".component-findings")!.TextContent.Trim());

        [Then("{string} has {int} SBOMs stored")]
        public async Task ThenHasSbomsStored(string project, int count)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
            Assert.Equal(count, await db.Sboms.CountAsync(s => s.ProjectId == id));
        }

        // ---------------------------------------------------------- Helpers

        private async Task UploadAsync(string fileName, string version, byte[] content)
        {
            await OpenTabAsync();
            var form = _web.Page!.QuerySelector("#upload-sbom")
                ?? throw new Xunit.Sdk.XunitException("The Scans tab has no SBOM upload.");
            var fields = Hidden(form);
            fields["productVersion"] = version;
            await _web.PostFileAsync(form.GetAttribute("action")!, fields, fileName, content);
        }

        private async Task<IElement> ComponentAsync(string name)
        {
            if (_web.Page?.QuerySelector("#sbom-components") is null)
            {
                await OpenTabAsync();
            }

            return _web.Page!.QuerySelectorAll("#sbom-components tr.sbom-component").FirstOrDefault(r => r.GetAttribute("data-name") == name)
                ?? throw new Xunit.Sdk.XunitException($"The SBOM lists no component '{name}'.");
        }

        private IElement Sbom(string version)
            => _web.Page!.QuerySelectorAll("#sboms tr.sbom").FirstOrDefault(r => r.GetAttribute("data-version") == version)
               ?? throw new Xunit.Sdk.XunitException($"The Scans tab has no SBOM for version '{version}'.");

        private async Task OpenTabAsync(string? version = null)
        {
            await using var db = _web.CreateDbContext();
            var id = await db.Projects.Where(p => p.Name == Project).Select(p => p.Id).SingleAsync();
            await _web.OpenAsync($"/Projects/Details/{id}/scans{(version is null ? "" : $"?sbom={Uri.EscapeDataString(version)}")}");
        }

        // PostFileAsync and PostFormAsync add the form token themselves
        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]")
                .Where(i => i.GetAttribute("name") != "__RequestVerificationToken")
                .ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
