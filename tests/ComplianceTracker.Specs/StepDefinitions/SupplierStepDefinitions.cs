using System.Text;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-191: the Suppliers page (/Suppliers): form#add-supplier, #suppliers rows with data-supplier and data-state,
    // .supplier-detail, form.add-supplier-evidence
    [Binding]
    public class SupplierStepDefinitions
    {
        private const string Path = "/Suppliers";
        private readonly WebScenario _web;
        private HttpResponseMessage? _response;

        public SupplierStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [Given("I have added the supplier {string} delivering {string} with criticality {string} and the rule {string}")]
        public async Task GivenIHaveAddedTheSupplier(string name, string delivers, string criticality, string rule)
        {
            await _web.OpenAsync(Path);
            var form = _web.Page!.QuerySelector("form#add-supplier") ?? throw new Xunit.Sdk.XunitException("No form to add a supplier.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["name"] = name,
                ["delivers"] = delivers,
                ["criticality"] = criticality,
                ["rules"] = rule
            });
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors"));
        }

        [Given("{string} has the evidence {string} valid until {string}")]
        public async Task GivenHasTheEvidence(string supplier, string title, string validUntil)
        {
            await _web.OpenAsync(Path);
            var form = Row(supplier).QuerySelector("form.add-supplier-evidence")!;
            using var content = new MultipartFormDataContent
            {
                { new StringContent(_web.AntiforgeryToken()), "__RequestVerificationToken" },
                { new StringContent(form.QuerySelector("input[name='supplierId']")!.GetAttribute("value")!), "supplierId" },
                { new StringContent("Certificate"), "kind" },
                { new StringContent(title), "title" },
                { new StringContent(validUntil), "validUntil" },
                { new ByteArrayContent(Encoding.UTF8.GetBytes($"{title} of {supplier}")), "file", "evidence.txt" }
            };
            _web.Response = await _web.Client.PostAsync(form.GetAttribute("action")!, content);
            _web.Page = await WebScenario.ParseAsync(_web.Response);
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors"));
        }

        [When("I open the suppliers")]
        public Task WhenIOpenTheSuppliers() => _web.OpenAsync(Path);

        [When("{string} tries to download the evidence {string} of {string}")]
        public async Task WhenTriesToDownload(string userName, string title, string supplier)
        {
            await using var db = _web.CreateDbContext();
            var row = await db.Suppliers.Where(s => s.Name == supplier)
                .SelectMany(s => s.Evidence.Where(e => e.Title == title).Select(e => new { SupplierId = s.Id, EvidenceId = e.Id })).SingleAsync();
            using var client = _web.CreateClient(signedInAs: userName);
            _response = await client.GetAsync($"{Path}/File?supplierId={row.SupplierId}&supplierEvidenceId={row.EvidenceId}");
        }

        [Then("{string} gets {string}")]
        public void ThenGets(string userName, string result)
        {
            Assert.Equal("not found", result);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, _response!.StatusCode);
        }

        [Then("the supplier {string} is {string} saying {string}")]
        public void ThenTheSupplierIs(string supplier, string state, string detail)
        {
            var row = Row(supplier);
            Assert.Equal(state, row.GetAttribute("data-state"));
            Assert.Equal(detail, row.QuerySelector(".supplier-detail")?.TextContent.Trim());
        }

        [Then("there is no form to add a supplier")]
        public void ThenNoForm() => Assert.Null(_web.Page!.QuerySelector("form#add-supplier"));

        private AngleSharp.Dom.IElement Row(string supplier)
            => _web.Page!.QuerySelector($"#suppliers tr[data-supplier='{supplier}']")
               ?? throw new Xunit.Sdk.XunitException($"No supplier '{supplier}' on {_web.CurrentPath}.");
    }
}
