using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ComplianceTracker.Specs.Support
{
    // What the web tests share within one scenario: the web app, the "browser" and the page shown last.
    // Reqnroll creates one instance per scenario and gives it to every step class that asks for it.
    public sealed class WebScenario : IDisposable
    {
        private readonly DatabaseScenario _database;
        private TestApp? _app;
        private HttpClient? _client;

        // CVR numbers for organisations created by the tests; each one is unique in the scenario
        private int _nextCvr = 10000000;

        public WebScenario(DatabaseScenario database)
        {
            _database = database;
        }

        // The last response and its HTML, parsed so the tests can look for elements
        public HttpResponseMessage? Response { get; set; }
        public IDocument? Page { get; set; }

        // Started the first time it is used, after "Given an empty database" has set the connection string
        public TestApp App => _app ??= TestApp.WithTestSignIn(_database.ConnectionString);

        // The client follows redirects and remembers cookies, like a browser
        public HttpClient Client => _client ??= App.Factory.CreateClient();

        // Signs the client in as this user (see TestAuthHandler). The e-mail is optional.
        public void SignInAs(string name, string? email = null)
        {
            Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
            Client.DefaultRequestHeaders.Remove(TestAuthHandler.EmailHeader);
            Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, name);
            if (email is not null)
            {
                Client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
            }
        }

        // An extra client, e.g. an attacker or a client that must not follow redirects
        public HttpClient CreateClient(string? signedInAs = null, bool followRedirects = true)
        {
            var client = App.Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = followRedirects
            });

            if (signedInAs is not null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, signedInAs);
            }

            return client;
        }

        // Opens a page, like typing the address in the browser
        public async Task OpenAsync(string path)
        {
            Response = await Client.GetAsync(path);
            Page = await ParseAsync(Response);
        }

        // Sends a form with the antiforgery token from the page shown last
        public async Task PostFormAsync(string path, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = AntiforgeryToken();
            Response = await Client.PostAsync(path, new FormUrlEncodedContent(fields));
            Page = await ParseAsync(Response);
        }

        // The hidden antiforgery token in the current page's form, with a clear error if there is none
        public string AntiforgeryToken()
     => Page?.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")?.Value
        ?? throw new Xunit.Sdk.XunitException(
            $"No antiforgery token on the page {CurrentPath} (status {(int)Response!.StatusCode}). Does the page exist and contain a form?");

        // The path of the page the client ended on, after any redirects
        public string CurrentPath => Response!.RequestMessage!.RequestUri!.AbsolutePath;

        // Turns the HTML into a document that can be searched with CSS selectors (AngleSharp)
        public static async Task<IDocument> ParseAsync(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            return await context.OpenAsync(request => request.Content(html));
        }

        // Direct access to the test database, for setting up data and checking results
        public ComplianceTrackerDbContext CreateDbContext() => TestDatabase.CreateContext(_database.ConnectionString);

        // Creates an organisation directly in the database, as if the user had done it in the app
        public async Task<Organization> CreateOrganisationAsync(string creatorName, string organisationName, string? cvr = null)
        {
            await using var db = CreateDbContext();
            var service = new OrganizationService(
                new OrganizationRepository(db),
                new ActivityLogRepository(db),
                new UnitOfWork(db));

            return await service.CreateAsync(
                organisationName,
                cvr ?? (_nextCvr++).ToString(),
                Sector.Manufacturing,
                OrganizationSize.Small,
                TestUsers.Identity(creatorName));
        }

        // Sends a form with a file, like a browser does when uploading (multipart/form-data).
        // The content type is always application/pdf, also for fake files: an attacker can claim anything,
        // so the server must look inside the file itself.
        public async Task PostFileAsync(string path, Dictionary<string, string> fields, string fileName, byte[] content)
        {
            using var form = new MultipartFormDataContent();
            foreach (var (name,value) in fields)
            {
                form.Add(new StringContent(value), name);
            }

            form.Add(new StringContent(AntiforgeryToken()), "__RequestVerificationToken");

            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            form.Add(file, "File", fileName);

            Response = await Client.PostAsync(path, form);
            Page = await ParseAsync(Response);
        }

        // Called by Reqnroll at the end of the scenario
        public void Dispose()
        {
            _client?.Dispose();
            _app?.Dispose();
        }
    }
}
