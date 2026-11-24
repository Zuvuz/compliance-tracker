using System.Text;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-214: the inventory of information assets (/Isms/Assets). UI contract: Views/Isms/Assets.cshtml (#assets rows with
    // data-asset, .asset-owner, .asset-classification; form#add-asset, form.change-asset, form#classification-scheme-form,
    // form#import-assets, a#assets-excel, #assets-read-only), the risk page's #risk-classification and the Suppliers page's
    // .supplier-assets. The forms are sent to their actions with the ids from the database.
    [Binding]
    public class AssetStepDefinitions
    {
        private const string Path = "/Isms/Assets";
        private readonly WebScenario _web;
        private HttpResponseMessage? _download;

        public AssetStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [Given("I add the asset {string} of the kind {string} owned by {string} as {string}")]
        [When("I add the asset {string} of the kind {string} owned by {string} as {string}")]
        public Task WhenIAddTheAsset(string name, string kind, string owner, string level) => AddAsync(name, kind, owner, level, false, [], []);

        [Given("I add the asset {string} of the kind {string} owned by {string} as {string} with personal data")]
        public Task WhenIAddTheAssetWithPersonalData(string name, string kind, string owner, string level) => AddAsync(name, kind, owner, level, true, [], []);

        [When("I add the asset {string} of the kind {string} owned by {string} as {string} hosted by {string} and concerned by the risk {string}")]
        public async Task WhenIAddTheLinkedAsset(string name, string kind, string owner, string level, string supplier, string risk)
            => await AddAsync(name, kind, owner, level, false, [await SupplierIdAsync(supplier)], [await RiskIdAsync(risk)]);

        [Given("the asset {string} is owned by {string}")]
        public Task GivenTheAssetIsOwnedBy(string name, string owner) => AddAsync(name, "Information", owner, "Confidential", false, [], []);

        [When("I link the asset {string} to the risk {string}")]
        public async Task WhenILinkTheAssetToTheRisk(string name, string risk)
        {
            await _web.OpenAsync(Path);
            await using var db = _web.CreateDbContext();
            var asset = await db.Assets.SingleAsync(a => a.Name == name);
            var fields = new List<KeyValuePair<string, string>>
            {
                new("assetId", asset.Id.ToString()),
                new("name", asset.Name),
                new("kind", asset.Kind.ToString()),
                new("ownerId", asset.OwnerId.ToString()),
                new("classification", asset.Classification),
                new("riskIds", (await RiskIdAsync(risk)).ToString())
            };
            await _web.PostFormAsync("/Isms/ChangeAsset", fields);
            AssertNoError();
        }

        [When("I filter the inventory on personal data")]
        public Task WhenIFilterOnPersonalData() => _web.OpenAsync(Path + "?personalData=true");

        [When("I set the classification levels to {string}")]
        public async Task WhenISetTheLevels(string levels)
        {
            await _web.OpenAsync(Path);
            await _web.PostFormAsync("/Isms/ClassificationScheme", new Dictionary<string, string>
            {
                ["levels"] = string.Join("\n", levels.Split(',', StringSplitOptions.TrimEntries))
            });
        }

        [When("I import the assets file with the rows")]
        public async Task WhenIImport(DataTable table)
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(";", table.Header));
            foreach (var row in table.Rows)
            {
                csv.AppendLine(string.Join(";", table.Header.Select(h => row[h])));
            }

            await _web.OpenAsync(Path);
            await _web.PostFileAsync("/Isms/ImportAssets", [], "assets.csv", Encoding.UTF8.GetBytes(csv.ToString()));
        }

        [When("I download the inventory as Excel")]
        public async Task WhenIDownload()
        {
            await _web.OpenAsync(Path);
            var link = _web.Page!.QuerySelector("a#assets-excel")?.GetAttribute("href") ?? throw new Xunit.Sdk.XunitException("No link to the workbook.");
            _download = await _web.Client.GetAsync(link);
        }

        [When("I open the assets page")]
        public Task WhenIOpen() => _web.OpenAsync(Path);

        // ---------------------------------------------------------- Then

        [Then("the inventory shows {string} owned by {string} as {string}")]
        public async Task ThenTheInventoryShows(string name, string owner, string level)
        {
            await _web.OpenAsync(Path);
            var row = Row(name);
            Assert.Equal(owner, row.QuerySelector(".asset-owner")?.TextContent.Trim());
            Assert.Equal(level, row.QuerySelector(".asset-classification")?.TextContent.Trim());
        }

        [Then("the asset {string} is marked {string}")]
        public async Task ThenTheAssetIsMarked(string name, string mark)
        {
            await _web.OpenAsync(Path);
            Assert.Equal(mark, Row(name).QuerySelector(".asset-owner")?.TextContent.Trim());
        }

        [Then("the risk {string} shows the classification {string}")]
        public async Task ThenTheRiskShows(string risk, string level)
        {
            await _web.OpenAsync($"/Risks/Details/{await RiskIdAsync(risk)}");
            Assert.Equal(level, _web.Page!.QuerySelector("#risk-classification")?.TextContent.Trim());
        }

        [Then("the supplier {string} shows the asset {string}")]
        public async Task ThenTheSupplierShows(string supplier, string asset)
        {
            await _web.OpenAsync("/Suppliers");
            var assets = _web.Page!.QuerySelector($"#suppliers tr[data-supplier='{supplier}'] .supplier-assets")?.TextContent ?? "";
            Assert.Contains(asset, assets, StringComparison.Ordinal);
        }

        [Then("the inventory lists only {string}")]
        public void ThenOnly(string name)
            => Assert.Equal([name], _web.Page!.QuerySelectorAll("#assets tr[data-asset]").Select(r => r.GetAttribute("data-asset")));

        [Then("the assets page says {string}")]
        public void ThenThePageSays(string text)
            => Assert.Contains(text, _web.Page!.QuerySelector(".validation-summary-errors")?.TextContent ?? _web.Page.Body!.TextContent, StringComparison.Ordinal);

        [Then("the inventory has no assets")]
        public async Task ThenNoAssets()
        {
            await using var db = _web.CreateDbContext();
            Assert.False(await db.Assets.AnyAsync());
        }

        [Then("the download is an Excel workbook")]
        public async Task ThenExcel()
        {
            Assert.True(_download!.IsSuccessStatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _download.Content.Headers.ContentType?.MediaType);
            var bytes = await _download.Content.ReadAsByteArrayAsync();
            Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));
        }

        [Then("the assets page is read-only")]
        public void ThenReadOnly()
        {
            Assert.NotNull(_web.Page!.QuerySelector("#assets-read-only"));
            Assert.Null(_web.Page.QuerySelector("form#add-asset, form.change-asset, form#classification-scheme-form, form#import-assets"));
        }

        // ---------------------------------------------------------- Helpers

        private async Task AddAsync(string name, string kind, string owner, string level, bool personal, Guid[] suppliers, Guid[] risks)
        {
            await _web.OpenAsync(Path);
            Guid ownerId;
            await using (var db = _web.CreateDbContext())
            {
                ownerId = await db.Organizations.SelectMany(o => o.Users).Where(u => u.Name == owner).Select(u => u.Id).FirstAsync();
            }

            var fields = new List<KeyValuePair<string, string>>
            {
                new("name", name),
                new("kind", kind),
                new("ownerId", ownerId.ToString()),
                new("classification", level)
            };
            if (personal)
            {
                fields.Add(new("personalData", "true"));
            }

            fields.AddRange(suppliers.Select(s => new KeyValuePair<string, string>("supplierIds", s.ToString())));
            fields.AddRange(risks.Select(r => new KeyValuePair<string, string>("riskIds", r.ToString())));
            await _web.PostFormAsync("/Isms/AddAsset", fields);
        }

        private void AssertNoError() => Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors li"));

        private async Task<Guid> RiskIdAsync(string title)
        {
            await using var db = _web.CreateDbContext();
            return await db.Risks.Where(r => r.Title == title).Select(r => r.Id).SingleAsync();
        }

        private async Task<Guid> SupplierIdAsync(string name)
        {
            await using var db = _web.CreateDbContext();
            return await db.Suppliers.Where(s => s.Name == name).Select(s => s.Id).SingleAsync();
        }

        private AngleSharp.Dom.IElement Row(string name)
            => _web.Page!.QuerySelector($"#assets tr[data-asset='{name}']")
               ?? throw new Xunit.Sdk.XunitException($"No asset '{name}' in the inventory.");
    }
}
