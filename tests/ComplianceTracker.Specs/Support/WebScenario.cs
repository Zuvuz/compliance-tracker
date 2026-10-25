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
    // Det, webtestene deler i ét scenarie: webappen, "browseren" og den side, der sidst blev vist.
    // Reqnroll laver én instans pr. scenarie og giver den til alle step-klasser, der beder om den.
    public sealed class WebScenario : IDisposable
    {
        private readonly DatabaseScenario _database;
        private TestApp? _app;
        private HttpClient? _client;
        private int _nextCvr = 10000000;

        public WebScenario(DatabaseScenario database)
        {
            _database = database;
        }

        public HttpResponseMessage? Response { get; set; }
        public IDocument? Page { get; set; }

        public TestApp App => _app ??= TestApp.WithTestSignIn(_database.ConnectionString);

        // Klienten følger redirects og husker cookies, som en browser
        public HttpClient Client => _client ??= App.Factory.CreateClient();

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

        // En ekstra klient, fx en angriber eller en klient, der ikke må følge redirects
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

        public async Task OpenAsync(string path)
        {
            Response = await Client.GetAsync(path);
            Page = await ParseAsync(Response);
        }

        // Sender en formular med antiforgery-tokenet fra den side, der sidst blev vist
        public async Task PostFormAsync(string path, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = AntiforgeryToken();
            Response = await Client.PostAsync(path, new FormUrlEncodedContent(fields));
            Page = await ParseAsync(Response);
        }

        public string AntiforgeryToken()
     => Page?.QuerySelector<IHtmlInputElement>("input[name='__RequestVerificationToken']")?.Value
        ?? throw new Xunit.Sdk.XunitException(
            $"No antiforgery token on the page {CurrentPath} (status {(int)Response!.StatusCode}). Does the page exist and contain a form?");

        // Stien på den side, klienten endte på efter eventuelle redirects
        public string CurrentPath => Response!.RequestMessage!.RequestUri!.AbsolutePath;

        public static async Task<IDocument> ParseAsync(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            return await context.OpenAsync(request => request.Content(html));
        }

        public ComplianceTrackerDbContext CreateDbContext() => TestDatabase.CreateContext(_database.ConnectionString);

        // Opretter en organisation direkte i databasen, som om brugeren havde gjort det
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

        public void Dispose()
        {
            _client?.Dispose();
            _app?.Dispose();
        }
    }
}
