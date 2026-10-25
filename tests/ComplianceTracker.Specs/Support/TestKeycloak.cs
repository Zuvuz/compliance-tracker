using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Testcontainers.Keycloak;

namespace ComplianceTracker.Specs.Support
{
    // Starter én Keycloak-container til hele testkørslen med realmen fra keycloak/compliance-tracker-realm.json.
    // Client secret og testbrugere hentes og oprettes via Keycloaks admin-API under testen,
    // så der ligger hverken secrets eller kodeord i repoet (US-004 R7).
    public static class TestKeycloak
    {
        public const string Realm = "compliance-tracker";
        public const string ClientId = "compliance-tracker-web";

        // Kun til den midlertidige testcontainer, som slettes efter testkørslen
        private const string AdminUser = "admin";
        private const string AdminPassword = "admin";

        private static readonly SemaphoreSlim Lock = new(1, 1);
        private static readonly Dictionary<string, string> Passwords = new();
        private static KeycloakContainer? _container;
        private static string _baseAddress = "";

        public static string Authority => $"{_baseAddress}/realms/{Realm}";
        public static string ClientSecret { get; private set; } = "";

        public static async Task StartAsync()
        {
            await Lock.WaitAsync();
            try
            {
                if (_container is not null)
                {
                    return;
                }

                var realmFile = Path.Combine(RepositoryRoot(), "keycloak", "compliance-tracker-realm.json");

                _container = new KeycloakBuilder("quay.io/keycloak/keycloak:26.3")
                    .WithUsername(AdminUser)
                    .WithPassword(AdminPassword)
                    .WithResourceMapping(new FileInfo(realmFile), "/opt/keycloak/data/import/")
                    .WithCommand("--import-realm")
                    .Build();

                await _container.StartAsync();
                _baseAddress = _container.GetBaseAddress().TrimEnd('/');
                ClientSecret = await GetClientSecretAsync();
            }
            finally
            {
                Lock.Release();
            }
        }

        // Opretter en bruger, der kan logge ind med det samme. Kodeordet laves tilfældigt pr. testkørsel.
        public static async Task CreateUserAsync(string username, string email)
        {
            var password = Guid.NewGuid().ToString("N");
            using var admin = await AdminClientAsync();

            var response = await admin.PostAsJsonAsync($"{_baseAddress}/admin/realms/{Realm}/users", new
            {
                username,
                email,
                firstName = char.ToUpperInvariant(username[0]) + username[1..],
                lastName = "Test",
                enabled = true,
                emailVerified = true,
                credentials = new[] { new { type = "password", value = password, temporary = false } }
            });

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
