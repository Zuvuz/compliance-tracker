using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AngleSharp.Dom;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Infrastructure.Reports;
using ComplianceTracker.Specs.Support;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-132: signing locked reports on the Reports tab, the seal on the Settings page, and "signs for the management" on
    // the Users page, like the members do. Certificates are made in the test (self-signed), so nothing is downloaded.
    [Binding]
    public class SigningReportsStepDefinitions
    {
        private const string Password = "seal-password";
        private readonly WebScenario _web;
        private HttpResponseMessage? _signResponse;

        public SigningReportsStepDefinitions(WebScenario web)
        {
            _web = web;
        }

        // ---------------------------------------------------------- Given / When

        [Given("{string} signs for the management of {string}")]
        public async Task GivenSignsForManagement(string userName, string organisation)
        {
            await _web.OpenAsync("/Users");
            var row = _web.Page!.QuerySelector($"#organisation-users tr[data-member='{userName}']")
                ?? throw new Xunit.Sdk.XunitException($"'{userName}' is not on the Users page.");
            var form = row.QuerySelector("form.signs-for-management")!;
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Given("the organisation has uploaded the seal {string}")]
        [When("the organisation has uploaded the seal {string}")]
        public Task GivenTheSealIsUploaded(string name) => UploadSealAsync(Pfx(name, DateTimeOffset.UtcNow.AddYears(1)), Password);

        [When("the organisation uploads a seal that expired yesterday")]
        public Task WhenTheOrganisationUploadsAnExpiredSeal() => UploadSealAsync(Pfx("Acme ApS", DateTimeOffset.UtcNow.AddDays(-1)), Password);

        [When("the organisation uploads the seal {string} with the wrong password")]
        public Task WhenTheOrganisationUploadsWithTheWrongPassword(string name) => UploadSealAsync(Pfx(name, DateTimeOffset.UtcNow.AddYears(1)), "wrong");

        [Given("signatures are asked for on {string}")]
        public async Task GivenSignaturesAreAskedFor(string title)
        {
            var item = await LockedReportAsync(title);
            var form = item.QuerySelector("form.request-signatures") ?? throw new Xunit.Sdk.XunitException($"Signatures cannot be asked for on '{title}'.");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        [Given("{string} signs {string} as {string}")]
        [When("{string} signs {string} as {string}")]
        public async Task WhenSigns(string userName, string title, string role)
        {
            await SignAsync(userName, title, role, minutesAgo: 0, mustHaveButton: true);
            // Signing redirects back to the Reports tab; a sign-in challenge or an error would not
            Assert.Equal(System.Net.HttpStatusCode.Found, _signResponse!.StatusCode);
            Assert.EndsWith("/reports", _signResponse.Headers.Location!.OriginalString, StringComparison.Ordinal);
        }

        // The page offers no button; the request is sent anyway, so the server must refuse it (R3)
        [When("{string} tries to sign {string} as {string}")]
        public Task WhenTriesToSign(string userName, string title, string role) => SignAsync(userName, title, role, minutesAgo: 0, mustHaveButton: false);

        [When("{string} signed in {int} minutes ago and signs {string} as {string}")]
        public Task WhenSignedInLongAgoAndSigns(string userName, int minutes, string title, string role) => SignAsync(userName, title, role, minutes, mustHaveButton: true);

        [When("I seal {string}")]
        public async Task WhenISeal(string title)
        {
            var item = await LockedReportAsync(title);
            var form = item.QuerySelector("form.seal-report") ?? throw new Xunit.Sdk.XunitException($"'{title}' cannot be sealed: {item.TextContent}");
            await _web.PostFormAsync(form.GetAttribute("action")!, Hidden(form));
        }

        // ---------------------------------------------------------- Then

        [Then("{string} is sealed by {string}")]
        public async Task ThenIsSealedBy(string title, string organisation)
        {
            var item = await LockedReportAsync(title);
            Assert.NotNull(item.QuerySelector("a.sealed-report-download"));
            Assert.Contains(organisation, item.QuerySelector(".sealed-by")!.TextContent, StringComparison.Ordinal);
        }

        [Then("the sealed PDF has a valid signature over the whole file")]
        public async Task ThenTheSealedPdfIsValid()
        {
            var (valid, covers, signer) = PdfSigner.Verify(await SealedPdfAsync());
            Assert.True(valid);
            Assert.True(covers);
            Assert.Contains("Acme ApS", signer!.Subject, StringComparison.Ordinal);
        }

        [Then("the sealed PDF lists {string} and {string}")]
        public async Task ThenTheSealedPdfLists(string first, string second)
        {
            using var pdf = PdfDocument.Open(await SealedPdfAsync());
            var last = string.Join(" ", pdf.GetPage(pdf.NumberOfPages).GetWords().Select(w => w.Text));
            Assert.Contains(first, last, StringComparison.Ordinal);
            Assert.Contains(second, last, StringComparison.Ordinal);
            Assert.Contains("Locked report SHA-256", last, StringComparison.Ordinal);
        }

        [Then("the report has {int} signature(s)")]
        public async Task ThenTheReportHasSignatures(int count)
        {
            await using var db = _web.CreateDbContext();
            Assert.Equal(count, await db.ReportSignings.SelectMany(s => s.Signatures).CountAsync());
        }

        [Then("{string} is asked to sign in again")]
        public void ThenIsAskedToSignInAgain(string userName)
        {
            // A sign-in challenge: 401 or a redirect to the sign-in (with Keycloak: its login page, prompt=login), never the
            // redirect back to the Reports tab that follows a signature
            var location = _signResponse!.Headers.Location?.OriginalString ?? "";
            Assert.True(_signResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        || (_signResponse.StatusCode == System.Net.HttpStatusCode.Found && !location.EndsWith("/reports", StringComparison.Ordinal)),
                $"{userName} was not asked to sign in again: {_signResponse.StatusCode} {location}");
        }

        [Then("the Reports tab says {string}")]
        public async Task ThenTheReportsTabSays(string text)
        {
            await OpenReportsAsync();
            Assert.Contains(text, _web.Page!.QuerySelector("#locked-reports")!.TextContent, StringComparison.Ordinal);
        }

        [Then("I see the error that starts with {string}")]
        public void ThenISeeTheErrorThatStartsWith(string start)
            => Assert.Contains(_web.Page!.QuerySelectorAll(".validation-summary-errors li"), e => e.TextContent.Trim().StartsWith(start, StringComparison.Ordinal));

        [Then("\"Needs attention\" of {string} shows {string} for {string}")]
        public async Task ThenNeedsAttentionShowsFor(string project, string text, string userName)
        {
            using var client = _web.CreateClient(signedInAs: userName);
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{await ProjectIdAsync(project)}"));
            Assert.Contains(page.QuerySelectorAll("#needs-attention li"), li => li.TextContent.Contains(text, StringComparison.Ordinal));
        }

        // ---------------------------------------------------------- Helpers

        private async Task SignAsync(string userName, string title, string role, int minutesAgo, bool mustHaveButton)
        {
            using var client = _web.CreateClient(signedInAs: userName, followRedirects: false);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SignedInMinutesAgoHeader, minutesAgo.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var page = await WebScenario.ParseAsync(await client.GetAsync($"/Projects/Details/{await ProjectIdAsync("Acme Portal")}/reports"));
            var item = page.QuerySelectorAll("li.locked-report").FirstOrDefault(l => l.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"No locked report '{title}'.");
            var form = item.QuerySelectorAll("form.sign-report").FirstOrDefault(f => f.GetAttribute("data-role") == role);
            if (mustHaveButton)
            {
                Assert.NotNull(form);
            }
            else
            {
                Assert.Null(form);
            }

            var token = page.QuerySelector("input[name='__RequestVerificationToken']")!.GetAttribute("value")!;
            await using var db = _web.CreateDbContext();
            var snapshotId = await db.ReportSnapshots.Where(s => s.Title == title).Select(s => s.Id).SingleAsync();
            _signResponse = await client.PostAsync("/Projects/SignReport", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["projectId"] = (await ProjectIdAsync("Acme Portal")).ToString(),
                ["snapshotId"] = snapshotId.ToString(),
                ["role"] = role,
                ["__RequestVerificationToken"] = token
            }));
        }

        private async Task UploadSealAsync(byte[] pfx, string password)
        {
            await _web.OpenAsync("/Settings");
            await _web.PostFileAsync("/Settings/UploadSeal", new Dictionary<string, string> { ["password"] = password }, "seal.pfx", pfx);
        }

        private static byte[] Pfx(string name, DateTimeOffset notAfter)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest($"CN={name}, O={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(notAfter.AddYears(-2), notAfter);
            return certificate.Export(X509ContentType.Pfx, Password);
        }

        private async Task<byte[]> SealedPdfAsync()
        {
            await OpenReportsAsync();
            var href = _web.Page!.QuerySelector("a.sealed-report-download")!.GetAttribute("href");
            var response = await _web.Client.GetAsync(href);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<IElement> LockedReportAsync(string title)
        {
            await OpenReportsAsync();
            return _web.Page!.QuerySelectorAll("li.locked-report").FirstOrDefault(l => l.GetAttribute("data-title") == title)
                ?? throw new Xunit.Sdk.XunitException($"No locked report '{title}'.");
        }

        private async Task OpenReportsAsync() => await _web.OpenAsync($"/Projects/Details/{await ProjectIdAsync("Acme Portal")}/reports");

        private async Task<Guid> ProjectIdAsync(string project)
        {
            await using var db = _web.CreateDbContext();
            return await db.Projects.Where(p => p.Name == project).Select(p => p.Id).SingleAsync();
        }

        private static Dictionary<string, string> Hidden(IElement form)
            => form.QuerySelectorAll("input[type=hidden]").ToDictionary(i => i.GetAttribute("name")!, i => i.GetAttribute("value") ?? "");
    }
}
