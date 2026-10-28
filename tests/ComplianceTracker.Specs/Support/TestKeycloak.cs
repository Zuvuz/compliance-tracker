using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Testcontainers.Keycloak;

namespace ComplianceTracker.Specs.Support
{
    // Starts one Keycloak container for the whole test run, with the realm from keycloak/compliance-tracker-realm.json.
    // The client secret and the test users are fetched and created through Keycloak's admin API during the test,
    // so there are neither secrets nor passwords in the repo (US-004 R7).
    public static class TestKeycloak
    {
        public const string Realm = "compliance-tracker";
        public const string ClientId = "compliance-tracker-web";

        // Only for the temporary test container, which is deleted after the test run
        private const string AdminUser = "admin";
        private const string AdminPassword = "admin";

        private static readonly SemaphoreSlim Lock = new(1, 1);

        // The random password of each test user created in this run
        private static readonly Dictionary<string, string> Passwords = new();
        private static KeycloakContainer? _container;
        private static string _baseAddress = "";

        // The realm's address, which the web app uses as Authority
        public static string Authority => $"{_baseAddress}/realms/{Realm}";
        public static string ClientSecret { get; private set; } = "";

        // Starts the container the first time it is needed (it takes about a minute)
        public static async Task StartAsync()
        {
            await Lock.WaitAsync();
            try
            {
                if (_container is not null)
                {
                    return;
                }

                // The same realm file the developers use in docker-compose.yml
                var realmFile = Path.Combine(RepositoryRoot(), "keycloak", "compliance-tracker-realm.json");

                _container = new KeycloakBuilder("quay.io/keycloak/keycloak:26.3")
                    .WithUsername(AdminUser)
                    .WithPassword(AdminPassword)
                    .WithResourceMapping(new FileInfo(realmFile), "/opt/keycloak/data/import/")
                    .WithCommand("--import-realm")
                    .Build();

                await _container.StartAsync();
                _baseAddress = _container.GetBaseAddress().TrimEnd('/');

                // The realm file has no secret, so Keycloak generated one; read it through the admin API
                ClientSecret = await GetClientSecretAsync();
            }
            finally
            {
                Lock.Release();
            }
        }

        // Creates a user who can sign in straight away. The password is random for each test run.
        public static async Task CreateUserAsync(string username, string email)
        {
            var password = Guid.NewGuid().ToString("N");
            using var admin = await AdminClientAsync();

            var response = await admin.PostAsJsonAsync($"{_baseAddress}/admin/realms/{Realm}/users", new
            {
                username,
                email,
                // Keycloak requires first and last name; without them it asks the user during sign-in
                firstName = char.ToUpperInvariant(username[0]) + username[1..],
                lastName = "Test",
                enabled = true,
                emailVerified = true,
                credentials = new[] { new { type = "password", value = password, temporary = false } }
            });

            // Conflict = the user already exists from an earlier scenario in this run; keep its password
            if (response.StatusCode != HttpStatusCode.Conflict)
            {
                response.EnsureSuccessStatusCode();
                Passwords[username] = password;
            }
        }

        public static string PasswordFor(string username) => Passwords[username];

        public static async Task DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }

        // Looks up the client in the realm and reads its generated secret
        private static async Task<string> GetClientSecretAsync()
        {
            using var admin = await AdminClientAsync();

            var clients = await admin.GetFromJsonAsync<JsonElement>(
                $"{_baseAddress}/admin/realms/{Realm}/clients?clientId={ClientId}");
            var id = clients[0].GetProperty("id").GetString();

            var secret = await admin.GetFromJsonAsync<JsonElement>(
                $"{_baseAddress}/admin/realms/{Realm}/clients/{id}/client-secret");
            return secret.GetProperty("value").GetString()!;
        }

        // An HttpClient signed in to Keycloak's admin API as the container's admin user
        private static async Task<HttpClient> AdminClientAsync()
        {
            var client = new HttpClient();
            var tokenResponse = await client.PostAsync(
                $"{_baseAddress}/realms/master/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = "admin-cli",
                    ["username"] = AdminUser,
                    ["password"] = AdminPassword
                }));
            tokenResponse.EnsureSuccessStatusCode();

            var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
            return client;
        }

        // Walks up from the test's bin folder until it finds the solution file
        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ComplianceTracker.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not find the repository root (ComplianceTracker.slnx).");
        }
    }
}
