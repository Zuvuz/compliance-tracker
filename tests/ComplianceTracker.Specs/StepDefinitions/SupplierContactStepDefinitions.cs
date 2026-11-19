using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-207: on /Suppliers, form#add-supplier with the company's fields, and per supplier .supplier-company,
    // .supplier-contact (data-name, data-primary, data-security) with a.write-to-contact, form.add-supplier-contact,
    // .no-security-contact and a.ask-for-evidence
    [Binding]
    public class SupplierContactStepDefinitions
    {
        private const string Path = "/Suppliers";
        private readonly WebScenario _web;

        public SupplierContactStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        [When("I add the supplier {string} with the CVR {string} and the country {string}")]
        public async Task WhenIAddTheSupplier(string name, string cvr, string country)
        {
            await _web.OpenAsync(Path);
            var form = _web.Page!.QuerySelector("form#add-supplier") ?? throw new Xunit.Sdk.XunitException("No form to add a supplier.");
            await _web.PostFormAsync(form.GetAttribute("action")!, new Dictionary<string, string>
            {
                ["name"] = name,
                ["delivers"] = "Hosting",
                ["criticality"] = "High",
                ["rules"] = "NIS2 Art. 21(2)(d)",
                ["registrationNumber"] = cvr,
                ["country"] = country
            });
            Assert.Null(_web.Page!.QuerySelector(".validation-summary-errors"));
        }

        [When("I add the contact person {string} to {string} as security contact with the e-mail {string}")]
        public Task WhenIAddASecurityContact(string name, string supplier, string email)
            => AddContactAsync(supplier, name, "Security contact", email, primary: false, security: true);

        [Given("I add the contact person {string} to {string} as {string} with the e-mail {string}")]
        [When("I add the contact person {string} to {string} as {string} with the e-mail {string}")]
        public Task WhenIAddAContact(string name, string supplier, string role, string email)
            => role == "primary"
                ? AddContactAsync(supplier, name, "Account manager", email, primary: true, security: false)
                : AddContactAsync(supplier, name, role, email, primary: false, security: false);

        [Then("the supplier {string} shows the CVR {string} and the country {string}")]
        public async Task ThenTheSupplierShowsTheCompany(string supplier, string cvr, string country)
        {
            await _web.OpenAsync(Path);
            var company = Row(supplier).QuerySelector(".supplier-company")!;
            Assert.Equal($"CVR/VAT {cvr}", company.QuerySelector(".supplier-registration")?.TextContent.Trim());
            Assert.Equal(country, company.QuerySelector(".supplier-country")?.TextContent.Trim());
        }

        [Then("the supplier {string} shows {string} as security contact")]
        public async Task ThenTheSupplierShowsTheSecurityContact(string supplier, string name)
        {
            await _web.OpenAsync(Path);
            Assert.Equal("true", Contact(supplier, name).GetAttribute("data-security"));
            Assert.Null(Row(supplier).QuerySelector(".no-security-contact"));
        }

        [Then("the supplier {string} has {string} as primary contact")]
        public async Task ThenThePrimaryIs(string supplier, string name)
        {
            await _web.OpenAsync(Path);
            var primaries = Row(supplier).QuerySelectorAll(".supplier-contact[data-primary='true']").Select(c => c.GetAttribute("data-name")).ToList();
            Assert.Equal([name], primaries);
        }

        [Then("{string} to {string} opens an e-mail to {string} with the subject {string}")]
        public void ThenWriteOpensAnEmail(string link, string name, string email, string subject)
        {
            var write = _web.Page!.QuerySelectorAll(".supplier-contact").First(c => c.GetAttribute("data-name") == name).QuerySelector("a.write-to-contact")
                ?? throw new Xunit.Sdk.XunitException($"No \"{link}\" for {name}.");
            Assert.Equal(link, write.TextContent.Trim());
            Assert.Equal($"mailto:{email}?subject={Uri.EscapeDataString(subject)}", write.GetAttribute("href"));
        }

        [Then("{string} is marked {string}")]
        public void ThenTheSupplierIsMarked(string supplier, string mark)
            => Assert.Equal(mark, Row(supplier).QuerySelector(".no-security-contact")?.TextContent.Trim());

        [Then("{string} offers to ask {string} for updated evidence")]
        public void ThenItOffersToAsk(string supplier, string name)
        {
            var ask = Row(supplier).QuerySelector("a.ask-for-evidence") ?? throw new Xunit.Sdk.XunitException($"{supplier} offers nobody to ask.");
            Assert.Equal($"Ask {name} for it", ask.TextContent.Trim());
            Assert.Contains("subject=Updated%20evidence%20%E2%80%93%20Demo%20ApS", ask.GetAttribute("href"));
        }

        private async Task AddContactAsync(string supplier, string name, string role, string email, bool primary, bool security)
        {
            await _web.OpenAsync(Path);
            var form = Row(supplier).QuerySelector("form.add-supplier-contact") ?? throw new Xunit.Sdk.XunitException($"No form to add a contact to {supplier}.");
            var fields = new Dictionary<string, string>
            {
                ["supplierId"] = form.QuerySelector("input[name='supplierId']")!.GetAttribute("value")!,
                ["name"] = name,
                ["role"] = role,
                ["email"] = email,
                ["phone"] = ""
            };
            if (primary)
            {
                fields["primary"] = "true";
            }

            if (security)
            {
                fields["securityContact"] = "true";
            }

            await _web.PostFormAsync(form.GetAttribute("action")!, fields);
        }

        private AngleSharp.Dom.IElement Row(string supplier)
            => _web.Page!.QuerySelectorAll("#suppliers tr[data-supplier]").FirstOrDefault(r => r.GetAttribute("data-supplier") == supplier)
               ?? throw new Xunit.Sdk.XunitException($"No supplier {supplier}.");

        private AngleSharp.Dom.IElement Contact(string supplier, string name)
            => Row(supplier).QuerySelectorAll(".supplier-contact").FirstOrDefault(c => c.GetAttribute("data-name") == name)
               ?? throw new Xunit.Sdk.XunitException($"{supplier} has no contact person {name}.");
    }
}
