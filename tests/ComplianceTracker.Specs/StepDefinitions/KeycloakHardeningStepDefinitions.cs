using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-089: the realm's settings and their effect, read from a real Keycloak through its admin API
    [Binding]
    public class KeycloakHardeningStepDefinitions
    {
        [Given("Keycloak is started with the tracker's realm")]
        public Task GivenKeycloakIsStarted()
            => TestKeycloak.StartAsync();

        // R1
        [Then("Keycloak's brute force protection is on")]
        public async Task ThenBruteForceProtectionIsOn()
        {
            var realm = await TestKeycloak.RealmAsync();
            Assert.True(realm.GetProperty("bruteForceProtected").GetBoolean());
            Assert.False(realm.GetProperty("permanentLockout").GetBoolean());
            Assert.Equal(5, realm.GetProperty("failureFactor").GetInt32());
        }

        // R2
        [Then("Keycloak's password policy requires at least {int} characters")]
        public async Task ThenThePasswordPolicyRequires(int length)
        {
            var policy = (await TestKeycloak.RealmAsync()).GetProperty("passwordPolicy").GetString() ?? "";
            Assert.Contains($"length({length})", policy);
            Assert.Contains("notUsername", policy);
            Assert.Contains("passwordHistory(3)", policy);
        }

        // R3: Configure OTP is a default action, so every new user gets it
        [Then("Keycloak requires {string} to set up a one-time password")]
        public async Task ThenKeycloakRequiresOtp(string username)
        {
            var actions = (await TestKeycloak.UserAsync(username)).GetProperty("requiredActions").EnumerateArray().Select(a => a.GetString());
            Assert.Contains("CONFIGURE_TOTP", actions);
        }

        // R1: locked for a time, not for good
        [Then("Keycloak has locked {string} for now")]
        public async Task ThenKeycloakHasLocked(string username)
        {
            var status = await TestKeycloak.BruteForceStatusAsync(username);
            Assert.True(status.GetProperty("disabled").GetBoolean(), $"Keycloak has not locked {username}: {status}");
        }
    }
}
